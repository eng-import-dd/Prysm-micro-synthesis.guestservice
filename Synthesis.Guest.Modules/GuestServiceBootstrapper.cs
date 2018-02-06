using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using FluentValidation;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Responses;
using Newtonsoft.Json;
using StackExchange.Redis;
using Synthesis.Authentication;
using Synthesis.Authentication.Jwt;
using Synthesis.Cache;
using Synthesis.Cache.Redis;
using Synthesis.Configuration;
using Synthesis.Configuration.Infrastructure;
using Synthesis.Configuration.Shared;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.DocumentDB;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka;
using Synthesis.GuestService.ApiWrappers;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.Http;
using Synthesis.Http.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Nancy.MicroService.Authentication;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Owin.Security;
using Synthesis.PolicyEvaluator.Autofac;
using Synthesis.Serialization.Json;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.Modules;
using Synthesis.GuestService.Owin;
using Synthesis.GuestService.Utilities;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Tracking;
using Synthesis.Tracking.ApplicationInsights;
using Synthesis.Tracking.Web;
using IObjectSerializer = Synthesis.Serialization.IObjectSerializer;
using RequestHeaders = Synthesis.Http.Microservice.RequestHeaders;

namespace Synthesis.GuestService
{
    public class GuestServiceBootstrapper : AutofacNancyBootstrapper
    {
        public const string ServiceName = "Synthesis.GuestService";
        public const string ServiceNameShort = "guest";
        private const int RedisConnectRetryTimes = 30;
        private const int RedisConnectTimeoutInMilliseconds = 10 * 1000;
        private const int RedisSyncTimeoutInMilliseconds = 15 * 1000;
        public static readonly LogTopic DefaultLogTopic = new LogTopic(ServiceName);
        public static readonly LogTopic EventServiceLogTopic = new LogTopic($"{ServiceName}.EventHub");
        private static readonly Lazy<ILifetimeScope> LazyRootContainer = new Lazy<ILifetimeScope>(BuildRootContainer);

        public GuestServiceBootstrapper()
        {
            ApplicationContainer = RootContainer.BeginLifetimeScope();
        }

        /// <summary>
        ///     Gets container for this bootstrapper instance.
        /// </summary>
        public new ILifetimeScope ApplicationContainer { get; }

        /// <summary>
        ///     Gets the root injection container for this service.
        /// </summary>
        /// <value>
        ///     The root injection container for this service.
        /// </value>
        public static ILifetimeScope RootContainer => LazyRootContainer.Value;

        /// <inheritdoc />
        protected override Func<ITypeCatalog, NancyInternalConfiguration> InternalConfiguration
        {
            get { return NancyInternalConfiguration.WithOverrides(config => { config.Serializers = new[] { typeof(DefaultXmlSerializer), typeof(SynthesisJsonSerializer) }; }); }
        }

        protected override void ConfigureApplicationContainer(ILifetimeScope container)
        {
            base.ConfigureApplicationContainer(container);

            container.Update(builder =>
            {
                builder.RegisterType<MetadataRegistry>().As<IMetadataRegistry>().SingleInstance();

                // Change the default json serializer to use a different contract resolver
                builder.Register(c =>
                {
                    var serializer = new JsonSerializer
                    {
                        ContractResolver = new SynthesisModelContractResolver(),
                        Formatting = Formatting.None
                    };
                    return serializer;
                });
            });

            container
                .Resolve<ILoggerFactory>()
                .GetLogger(this)
                .Info("GuestService Service Running....");
        }

        protected override ILifetimeScope CreateRequestContainer(NancyContext context)
        {
            return ApplicationContainer.BeginLifetimeScope(
                MatchingScopeLifetimeTags.RequestLifetimeScopeTag,
                bldr =>
                {
                    bldr.Register(c => new RequestHeaders(context.Request.Headers))
                        .As<IRequestHeaders>()
                        .InstancePerLifetimeScope();
                });
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ApplicationContainer.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override ILifetimeScope GetApplicationContainer()
        {
            return ApplicationContainer;
        }

        private static ILifetimeScope BuildRootContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterType<DefaultAppSettingsReader>()
                .Keyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader));

            builder.RegisterType<SharedAppSettingsReader>()
                .As<IAppSettingsReader>()
                .As<ISharedAppSettingsReader>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "configurationServiceUrl",
                    (p, c) => c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader)).GetValue<string>("Configuration.Url")))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "httpClient",
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClient>(nameof(ServiceToServiceClient))))
                .SingleInstance();

            RegisterLogging(builder);

            // Tracking
            builder.RegisterType<ApplicationInsightsTrackingService>().As<ITrackingService>();

            // Register our custom OWIN Middleware
            builder.RegisterType<GlobalExceptionHandlerMiddleware>().InstancePerRequest();
            builder.RegisterType<CorrelationScopeMiddleware>().InstancePerRequest();
            builder.RegisterType<SynthesisAuthenticationMiddleware>().InstancePerRequest();
            builder
                .RegisterType<ImpersonateTenantMiddleware>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "tenantUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("Tenant.Url")))
                .InstancePerRequest();

            // DocumentDB registration.
            builder.Register(c =>
            {
                var settings = c.Resolve<IAppSettingsReader>();
                return new DocumentDbContext
                {
                    AuthKey = settings.GetValue<string>("Guest.DocumentDB.AuthKey"),
                    Endpoint = settings.GetValue<string>("Guest.DocumentDB.Endpoint"),
                    DatabaseName = settings.GetValue<string>("Guest.DocumentDB.DatabaseName")
                };
            });
            builder.RegisterType<DocumentDbRepositoryFactory>().As<IRepositoryFactory>().SingleInstance();

            builder.Register(c =>
            {
                var reader = c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader));
                return new ServiceToServiceClientConfiguration
                {
                    AuthenticationRoute = $"{reader.GetValue<string>("Identity.Url").TrimEnd('/')}/{reader.GetValue<string>("Identity.AccessTokenRoute").TrimStart('/')}",
                    ClientId = reader.GetValue<string>("Guest.Synthesis.ClientId"),
                    ClientSecret = reader.GetValue<string>("Guest.Synthesis.ClientSecret")
                };
            });

            // Certificate provider that provides the JWT validation key to the token validator.
            builder.RegisterType<IdentityServiceCertificateProvider>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "identityUrl",
                    (p, c) => c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader)).GetValue<string>("Identity.Url")))
                .As<ICertificateProvider>();

            // Microservice HTTP Clients
            builder.RegisterType<AuthorizationPassThroughClient>()
                .Keyed<IMicroserviceHttpClient>(nameof(AuthorizationPassThroughClient));

            builder.RegisterType<ServiceToServiceClient>()
                .Keyed<IMicroserviceHttpClient>(nameof(ServiceToServiceClient))
                .AsSelf();

            builder.RegisterType<SynthesisHttpClient>()
                .As<IHttpClient>();

            builder.RegisterType<HttpClientConfiguration>()
                .As<IHttpClientConfiguration>();

            // Object serialization
            builder.RegisterType<JsonObjectSerializer>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(JsonSerializer),
                    (p, c) => new JsonSerializer()))
                .As<IObjectSerializer>();

            // JWT Token Validator
            builder.RegisterType<JwtTokenValidator>()
                .As<ITokenValidator>()
                .SingleInstance();

            // Microservice HTTP client resolver that will select the proper implementation of
            // IMicroserviceHttpClient for calling other microservices.
            builder.RegisterType<MicroserviceHttpClientResolver>()
                .As<IMicroserviceHttpClientResolver>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "passThroughKey",
                    (p, c) => nameof(AuthorizationPassThroughClient)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceKey",
                    (p, c) => nameof(ServiceToServiceClient)));

            // Policy Evaluator components
            builder.RegisterPolicyEvaluatorComponents();

            // Redis cache
            builder.RegisterType<RedisCache>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IConnectionMultiplexer),
                    (p, c) =>
                    {
                        var reader = c.Resolve<IAppSettingsReader>();
                        var redisOptions = new ConfigurationOptions
                        {
                            Password = reader.GetValue<string>("Redis.General.Key"),
                            AbortOnConnectFail = false,
                            SyncTimeout = RedisSyncTimeoutInMilliseconds,
                            ConnectTimeout = RedisConnectTimeoutInMilliseconds,
                            ConnectRetry = RedisConnectRetryTimes
                        };
                        redisOptions.EndPoints.Add(reader.GetValue<string>("Redis.General.Endpoint"));
                        return ConnectionMultiplexer.Connect(redisOptions);
                    }))
                .As<ICache>()
                .SingleInstance();

            // Validation
            RegisterValidation(builder);

            RegisterEvents(builder);

            RegisterServiceSpecificRegistrations(builder);

            return builder.Build();
        }

        /// <summary>
        ///     The point of this method is to ease updating services.  Any registrations that a service needs can go into this
        ///     method and then when updating to the latest template, this can just be copied forward.
        /// </summary>
        /// <param name="builder"></param>
        private static void RegisterServiceSpecificRegistrations(ContainerBuilder builder)
        {
            // Api Wrappers
            builder.RegisterType<ProjectApiWrapper>()
                .As<IProjectApiWrapper>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(string) && p.Name == "serviceUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("ProjectService.Url")));

            builder.RegisterType<SettingsApiWrapper>()
                .As<ISettingsApiWrapper>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(string) && p.Name == "serviceUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("SynthesisCloud.Url")));

            builder.RegisterType<PrincipalApiWrapper>()
                .As<IPrincipalApiWrapper>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(string) && p.Name == "serviceUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("PrincipalService.Url")));

            builder.RegisterType<ParticipantApiWrapper>()
                .As<IParticipantApiWrapper>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(string) && p.Name == "serviceUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("ParticipantService.Url")));

            // Controllers
            builder.RegisterType<GuestInviteController>().As<IGuestInviteController>();
            builder.RegisterType<GuestSessionController>().As<IGuestSessionController>();
            builder.RegisterType<ProjectLobbyStateController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "maxGuestsAllowedInProject",
                    (p, c) => c.Resolve<IAppSettingsReader>().SafeGetValue<int>("MaxGuestsAllowedInProject")))
                .As<IProjectLobbyStateController>();

            // Utilities
            builder.RegisterType<EmailUtility>().As<IEmailUtility>();
            builder.RegisterType<PasswordUtility>().As<IPasswordUtility>();
        }

        private static void RegisterLogging(ContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                return CreateLogLayout(reader);
            }).SingleInstance();
            var loggerFactory = new LoggerFactory();
            var defaultLogger = loggerFactory.Get(DefaultLogTopic);
            builder.RegisterInstance(defaultLogger);
            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();
        }

        private static ILogLayout CreateLogLayout(IAppSettingsReader settingsReader)
        {
            var version = typeof(GuestServiceBootstrapper).Assembly.GetName().Version.ToString();

            var logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            var localIpHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            var messageContent = logLayout.Get<LogLayoutMetadata>();
            messageContent.LocalIP = localIpHostEntry.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = ServiceName;
            messageContent.Environment = settingsReader.GetValue<string>("Environment");
            messageContent.Facility = settingsReader.GetValue<string>("Guest.Facility");
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;

            logLayout.Update(messageContent);

            return logLayout;
        }

        private static void RegisterValidation(ContainerBuilder builder)
        {
            builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();

            // Use reflection to register all the IValidators in the Synthesis.GuestService.Validators namespace
            var assembly = Assembly.GetAssembly(typeof(GuestSessionModule));
            var types = assembly.GetTypes().Where(x => string.Equals(x.Namespace, "Synthesis.GuestService.Validators", StringComparison.Ordinal)).ToArray();
            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(IValidator).IsAssignableFrom(type))
                {
                    builder.RegisterType(type).AsSelf().As<IValidator>();
                }
            }
        }

        private static void RegisterEvents(ContainerBuilder builder)
        {
            builder
                .RegisterType<EventSubscriber>()
                .AsSelf()
                .AutoActivate();

            // Event Service registration.
            builder.RegisterType<EventServiceFactory>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "logger",
                    (p, c) => c.Resolve<ILoggerFactory>().Get(EventServiceLogTopic)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "context",
                    (p, c) => new EventServiceContext
                    {
                        ServiceName = ServiceName,
                        KafkaConnection = c.Resolve<IAppSettingsReader>().GetValue<string>("Kafka.Server"),
                        SchemaConnection = c.Resolve<IAppSettingsReader>().GetValue<string>("SchemaRegistry.Server")
                    }))
                .As<IEventServiceFactory>()
                .SingleInstance();

            builder.Register(c => c.Resolve<IEventServiceFactory>().CreateEventService(ServiceName))
                .As<IEventService>()
                .SingleInstance();

            builder
                .RegisterType<EventHandlerLocator>()
                .As<IEventHandlerLocator>()
                .SingleInstance()
                .AutoActivate();

            // Use reflection to register all the IEventHandlers in the Synthesis.GuestService.EventHandlers namespace
            var assembly = Assembly.GetAssembly(typeof(GuestSessionModule));
            var types = assembly.GetTypes().Where(x => string.Equals(x.Namespace, "Synthesis.GuestService.EventHandlers", StringComparison.Ordinal)).ToArray();
            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(IEventHandlerBase).IsAssignableFrom(type))
                {
                    builder.RegisterType(type).AsSelf().As<IEventHandlerBase>();
                }
            }

            // register event service for events to be handled for every instance of this service
            builder
                .Register(c => new EventHandlerLocator(
                    c.Resolve<IEventServiceFactory>().CreateEventService(ServiceName + Guid.NewGuid()),
                    new IEventHandlerBase[] { new SettingsInvalidateCacheEventHandler(c.Resolve<ISharedAppSettingsReader>()) }))
                .Keyed<IEventHandlerLocator>("PerServiceEventServiceKey")
                .SingleInstance()
                .AutoActivate();
        }
    }
}
