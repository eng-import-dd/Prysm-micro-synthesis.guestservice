using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using FluentValidation;
using Microsoft.Owin;
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
using Synthesis.DocumentStorage.Migrations;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka.Autofac;
using Synthesis.ExpirationNotifierService.InternalApi.Api;
using Synthesis.ExpirationNotifierService.InternalApi.Services;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Email;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.InternalApi.Api;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Modules;
using Synthesis.GuestService.Owin;
using Synthesis.GuestService.Utilities;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Http;
using Synthesis.Http.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Microservice.Health;
using Synthesis.Nancy.MicroService.Authentication;
using Synthesis.Nancy.MicroService.EventBus;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Middleware;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Owin.Security;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.PolicyEvaluator.Autofac;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.Serialization.Json;
using Synthesis.SettingService.InternalApi.Api;
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
        public const string ServiceToServiceProjectAccessApiKey = "ServiceToServiceProjectAccessApiKey";
        public const string ServiceToServiceProjectApiKey = "ServiceToServiceProjectApiKey";
        public const string ServiceToServiceTenantApiKey = "ServiceToServiceTenantApiKey";
        public const string ServiceToServiceSettingApiKey = "ServiceToServiceSettingApiKey";

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
                        ContractResolver = new ApiModelContractResolver(),
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
                    bldr.RegisterType<EventServicePublishExtender>()
                        .WithParameter(new ResolvedParameter(
                            (p, c) => p.ParameterType == typeof(IEventService),
                            (p, c) => ApplicationContainer.Resolve<IEventService>()))
                        .As<IEventService>()
                        .InstancePerLifetimeScope();

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

            RegisterRedisKeyed(builder, "Redis.General.Key", "Redis.General.Endpoint", "Redis.General.Ssl", CacheConnection.General, true);
            RegisterRedisKeyed(builder, "Redis.Refresh.Key", "Redis.Refresh.Endpoint", "Redis.Refresh.Ssl", CacheConnection.Refresh, false);
            RegisterRedisKeyed(builder, "Redis.ExpirationNotifier.Key", "Redis.ExpirationNotifier.Endpoint", "Redis.ExpirationNotifier.Ssl", CacheConnection.ExpirationNotifier, false);

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
            builder.RegisterType<ResourceNotFoundMiddleware>().InstancePerRequest();
            builder.RegisterType<GuestContextMiddleware>().InstancePerRequest();
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
                    DatabaseName = settings.GetValue<string>("Guest.DocumentDB.DatabaseName"),
                    RuThroughput = settings.GetValue<int>("Guest.DocumentDB.RuThroughput")
                };
            });

            builder.Register(c => new RepositoryMigrationsConfiguration
            {
                MigrationsAssembly = typeof(GuestServiceBootstrapper).Assembly,
                MigrationsNamespace = $"{typeof(GuestServiceBootstrapper).Namespace}.Migrations"
            });

            builder.RegisterType<DocumentClientFactory>().As<IDocumentClientFactory>().SingleInstance();

            builder.RegisterType<DocumentDbRepositoryFactory>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "migrationsConfiguration",
                    (p, c) => c.Resolve<RepositoryMigrationsConfiguration>()))
                .As<IRepositoryFactory>().SingleInstance();

            builder.RegisterType<DocumentFileReader>().As<IDocumentFileReader>();
            builder.RegisterType<DefaultDocumentDbConfigurationProvider>().As<IDocumentDbConfigurationProvider>();
            builder.RegisterInstance(Assembly.GetExecutingAssembly());

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

            builder.RegisterType<SynthesisHttpClient>().As<IHttpClient>().SingleInstance();

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

            // Validation
            RegisterValidation(builder);

            RegisterEvents(builder);

            RegisterServiceSpecificRegistrations(builder);

            // IRequestHeaders for ProjectGuestContext
            builder.Register(c =>
            {
                var owinContext = c.ResolveOptional<IOwinContext>();
                if (owinContext == null)
                {
                    return new RequestHeaders(Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>());
                }

                var headers = owinContext.Request.Headers
                    .Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.AsEnumerable()));

                return new RequestHeaders(headers);
            })
            .As<IRequestHeaders>()
            .InstancePerLifetimeScope();

            return builder.Build();
        }

        private static void RegisterRedisKeyed(ContainerBuilder builder, string passwordKey, string endpointKey, string sslKey, CacheConnection instanceKey, bool defaultInstance)
        {
            var builderP2 = builder.RegisterType<RedisCache>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IConnectionMultiplexer),
                    (p, c) =>
                    {
                        var reader = c.Resolve<IAppSettingsReader>();
                        var sslSetting = reader.SafeGetValue(sslKey, "false");
                        var result = bool.TryParse(sslSetting, out var ssl);
                        if (!result)
                        {
                            ssl = false;
                        }
                        var redisOptions = new ConfigurationOptions
                        {
                            Password = reader.GetValue<string>(passwordKey),
                            AbortOnConnectFail = false,
                            SyncTimeout = RedisSyncTimeoutInMilliseconds,
                            ConnectTimeout = RedisConnectTimeoutInMilliseconds,
                            ConnectRetry = RedisConnectRetryTimes,
                            Ssl = ssl
                        };
                        redisOptions.EndPoints.Add(reader.GetValue<string>(endpointKey));
                        return ConnectionMultiplexer.Connect(redisOptions);
                    }))
                .Keyed<ICache>(instanceKey)
                .SingleInstance();

            if (defaultInstance)
            {
                builderP2.As<ICache>().As<ITimeService>();
            }
        }

        /// <summary>
        ///     The point of this method is to ease updating services.  Any registrations that a service needs can go into this
        ///     method and then when updating to the latest template, this can just be copied forward.
        /// </summary>
        /// <param name="builder"></param>
        private static void RegisterServiceSpecificRegistrations(ContainerBuilder builder)
        {
            builder.RegisterType<CacheSelector>().As<ICacheSelector>().SingleInstance();
            builder.RegisterType<CacheNotificationService>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "generalCache",
                    (p, c) => c.ResolveKeyed<ICache>(CacheConnection.General)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "refreshCache",
                    (p, c) => c.ResolveKeyed<ICache>(CacheConnection.Refresh)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "lockedCache",
                    (p, c) => c.ResolveKeyed<ICache>(CacheConnection.ExpirationNotifier)))
                .As<INotificationService>().SingleInstance();

            // Service To Service Resolver
            builder.RegisterType<ServiceToServiceMicroserviceHttpClientResolver>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IMicroserviceHttpClientResolver),
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClientResolver>(nameof(ServiceToServiceClient))))
                .Keyed<IMicroserviceHttpClientResolver>(nameof(ServiceToServiceMicroserviceHttpClientResolver))
                .InstancePerRequest();

            // Apis
            builder.RegisterType<ProjectApi>().As<IProjectApi>();

            builder.RegisterType<ProjectApi>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IMicroserviceHttpClientResolver),
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClientResolver>(nameof(ServiceToServiceMicroserviceHttpClientResolver))))
                .Keyed<IProjectApi>(ServiceToServiceProjectApiKey);

            builder.RegisterType<ProjectAccessApi>().As<IProjectAccessApi>();

            builder.RegisterType<ProjectAccessApi>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IMicroserviceHttpClientResolver),
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClientResolver>(nameof(ServiceToServiceMicroserviceHttpClientResolver))))
                .Keyed<IProjectAccessApi>(ServiceToServiceProjectAccessApiKey);

            builder.RegisterType<SettingApi>().As<ISettingApi>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IMicroserviceHttpClientResolver),
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClientResolver>(nameof(ServiceToServiceMicroserviceHttpClientResolver))))
                .Keyed<ISettingApi>(ServiceToServiceSettingApiKey);

            builder.RegisterType<UserApi>().As<IUserApi>();
            builder.RegisterType<ProjectGuestContextService>().As<IProjectGuestContextService>();

            // Controllers
            builder.RegisterType<GuestTenantController>().As<IGuestTenantController>();
            builder.RegisterType<GuestInviteController>().As<IGuestInviteController>();

            builder.RegisterType<GuestSessionController>().As<IGuestSessionController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceAccountSettingApi",
                    (p, c) => c.ResolveKeyed<ISettingApi>(ServiceToServiceSettingApiKey)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceProjectApi",
                    (p, c) => c.ResolveKeyed<IProjectApi>(ServiceToServiceProjectApiKey)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "maxGuestsAllowedInProject",
                    (p, c) => c.Resolve<IAppSettingsReader>().SafeGetValue<int>("Guest.MaxGuestsAllowedInProject")));

            builder.RegisterType<ProjectLobbyStateController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "maxGuestsAllowedInProject",
                    (p, c) => c.Resolve<IAppSettingsReader>().SafeGetValue<int>("Guest.MaxGuestsAllowedInProject")))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceProjectApi",
                    (p, c) => c.ResolveKeyed<IProjectApi>(ServiceToServiceProjectApiKey)))
                .As<IProjectLobbyStateController>();

            builder.RegisterType<ProjectGuestContextController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceProjectAccessApi",
                    (p, c) => c.ResolveKeyed<IProjectAccessApi>(ServiceToServiceProjectAccessApiKey)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceProjectApi",
                    (p, c) => c.ResolveKeyed<IProjectApi>(ServiceToServiceProjectApiKey)))
                .As<IProjectGuestContextController>();

            // Utilities
            builder.RegisterType<EmailBuilder>().As<IEmailBuilder>();
            builder.RegisterType<PasswordUtility>().As<IPasswordUtility>();

            builder.RegisterType<SessionService>().As<ISessionService>();

            builder.RegisterType<RepositoryHealthReporter<GuestInvite>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceNameShort);
            builder.RegisterType<RepositoryHealthReporter<GuestSession>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceNameShort);

            builder.RegisterType<EmailApi>().As<IEmailApi>();
            builder.RegisterType<EmailSendingService>().As<IEmailSendingService>();
            builder.RegisterType<ExpirationNotifierApi>().As<IExpirationNotifierApi>();

            // TODO: Replace IGuestApi implementation with an internal version that calls controllers.
            builder.RegisterType<GuestApi>().As<IGuestApi>();
        }

        private static void RegisterLogging(ContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                return CreateLogLayout(reader);
            }).AutoActivate();
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
            // Event Service registration.
            builder.RegisterKafkaEventBusComponents(
                ServiceName,
                (metadata, bldr) =>
                {
                    bldr.RegisterType<EventServicePublishExtender>()
                        .WithParameter(new ResolvedParameter(
                            (p, c) => p.ParameterType == typeof(IEventService),
                            (p, c) => RootContainer.Resolve<IEventService>()))
                        .As<IEventService>()
                        .InstancePerLifetimeScope();

                    bldr.Register(c => metadata.ToRequestHeaders())
                        .InstancePerRequest();
                });

            builder
                .RegisterType<EventSubscriber>()
                .AsSelf()
                .AutoActivate();

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
            builder.RegisterType<SettingsInvalidateCacheEventHandler>().AsSelf();

            builder.RegisterType<EventHandlerLocator>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IEventServiceConsumer),
                    (p, c) => c.ResolveKeyed<IEventServiceConsumer>(Registration.PerInstanceEventServiceKey)))
                .OnActivated(args => args.Instance.SubscribeEventHandler<SettingsInvalidateCacheEventHandler>("*", EventNames.SettingsInvalidateCache))
                .Keyed<IEventHandlerLocator>(Registration.PerInstanceEventServiceKey)
                .SingleInstance()
                .AutoActivate();
        }
    }
}