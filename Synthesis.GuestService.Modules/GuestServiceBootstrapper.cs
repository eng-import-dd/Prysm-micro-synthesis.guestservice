using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using FluentValidation;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Synthesis.Configuration;
using Synthesis.Configuration.Infrastructure;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.DocumentDB;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka;
using Synthesis.GuestService.Owin;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.Http;
using Synthesis.Http.Microservice;
using Synthesis.KeyManager;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Serialization.Json;
using Synthesis.Tracking;
using Synthesis.Tracking.ApplicationInsights;
using Synthesis.Tracking.Web;
using IValidatorLocator = Synthesis.Nancy.MicroService.Validation.IValidatorLocator;
using RequestHeaders = Synthesis.Http.Microservice.RequestHeaders;
using ValidatorLocator = Synthesis.Nancy.MicroService.Validation.ValidatorLocator;

namespace Synthesis.GuestService
{
    public class GuestServiceBootstrapper : AutofacNancyBootstrapper
    {
        public static readonly LogTopic DefaultLogTopic = new LogTopic("Synthesis.GuestService");
        public static readonly LogTopic EventServiceLogTopic = new LogTopic("Synthesis.GuestService.EventHub");
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

        protected override ILifetimeScope CreateRequestContainer(NancyContext context)
        {
            return ApplicationContainer.BeginLifetimeScope(MatchingScopeLifetimeTags.RequestLifetimeScopeTag,
                                                           bldr =>
                                                           {
                                                               bldr.Register(c => new RequestHeaders(context.Request.Headers))
                                                                   .As<IRequestHeaders>()
                                                                   .InstancePerLifetimeScope();
                                                           });
        }

        /// <summary>
        ///     Gets a logger using the default log topic for this service.
        /// </summary>
        public ILogger GetDefaultLogger()
        {
            return ApplicationContainer.Resolve<ILogger>();
        }

        protected override void ConfigureApplicationContainer(ILifetimeScope container)
        {
            base.ConfigureApplicationContainer(container);

            container.Update(builder =>
                             {
                                 builder.RegisterType<MetadataRegistry>().As<IMetadataRegistry>().SingleInstance();

                                 // Update this registration if you need to change the authorization implementation.
                                 builder.Register(c => new SynthesisStatelessAuthorization(c.Resolve<IKeyManager>(), c.Resolve<ILogger>()))
                                        .As<IStatelessAuthorization>()
                                        .SingleInstance();
                             });

            container.Resolve<ILogger>().Info("GuestService Service Running....");
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            // Add the micro-service authorization logic to the Nancy pipeline.
            pipelines.BeforeRequest += ctx =>
                                       {
                                           // TODO: This is temporary until we get JWT implemented.
                                           var identity = new ClaimsIdentity(
                                                                             new[]
                                                                             {
                                                                                 new Claim(ClaimTypes.Name, "Test User"),
                                                                                 new Claim(ClaimTypes.Email, "test@user.com")
                                                                             },
                                                                             AuthenticationTypes.Basic);
                                           ctx.CurrentUser = new ClaimsPrincipal(identity);
                                           return null;
                                       };

            base.ApplicationStartup(container, pipelines);

            //
            //            Metric.Config
            //                .WithAllCounters()
            //                .WithHttpEndpoint("http://localhost:9000/metrics/")
            //                .WithInternalMetrics()
            //                .WithNancy(pipelines);
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

            var settingsReader = new DefaultAppSettingsReader();
            var loggerFactory = new LoggerFactory();
            var defaultLogger = loggerFactory.Get(DefaultLogTopic);

            builder.RegisterInstance(settingsReader).As<IAppSettingsReader>();

            // Logging
            builder.RegisterInstance(CreateLogLayout(settingsReader));
            builder.RegisterInstance(defaultLogger);
            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();

            RegisterHttpClient(builder, settingsReader);

            // Tracking
            builder.RegisterType<ApplicationInsightsTrackingService>().As<ITrackingService>();

            // Register our custom OWIN Middleware
            builder.RegisterType<GlobalExceptionHandlerMiddleware>().InstancePerRequest();
            builder.RegisterType<CorrelationScopeMiddleware>().InstancePerRequest();

            // Event Service registration.
            builder.Register(
                             c =>
                             {
                                 var connectionString = c.Resolve<IAppSettingsReader>().GetValue<string>("Kafka.Server");
                                 return EventBus.Kafka.EventBus.Create(connectionString).CreateEventPublisher();
                             })
                   .As<IEventPublisher>();
            builder.Register(c => new EventServiceContext { ServiceName = "Synthesis.GuestService" });
            builder.RegisterType<EventService>().As<IEventService>().SingleInstance()
                   .WithParameter(new ResolvedParameter(
                                                        (p, c) => p.Name == "logger",
                                                        (p, c) => c.Resolve<ILoggerFactory>().Get(EventServiceLogTopic)));

            // DocumentDB registration.
            builder.Register(c =>
                             {
                                 var settings = c.Resolve<IAppSettingsReader>();
                                 return new DocumentDbContext
                                 {
                                     AuthKey = settings.GetValue<string>("DocumentDB.AuthKey"),
                                     DatabaseName = settings.GetValue<string>("DocumentDB.DatabaseName"),
                                     Endpoint = settings.GetValue<string>("DocumentDB.Endpoint")
                                 };
                             });
            builder.RegisterType<DocumentDbRepositoryFactory>().As<IRepositoryFactory>().SingleInstance();

            // Key Manager
            builder.RegisterType<SimpleKeyManager>().As<IKeyManager>().SingleInstance();

            // Validation
            builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();
            builder.RegisterType<GuestInviteIdValidator>().As<IValidator>();
            builder.RegisterType<GuestInviteValidator>().As<IValidator>();
            builder.RegisterType<GuestSessionIdValidator>().As<IValidator>();
            builder.RegisterType<GuestSessionValidator>().As<IValidator>();

            // Controllers
            builder.RegisterType<GuestInviteController>().As<IGuestInviteController>();
            builder.RegisterType<GuestSessionController>().As<IGuestSessionController>();

            return builder.Build();
        }

        private static void RegisterHttpClient(ContainerBuilder builder, DefaultAppSettingsReader settingsReader)
        {
            builder.RegisterType<SynthesisHttpClient>()
                   .As<IHttpClient>()
                   .SingleInstance();
            builder.RegisterType<JsonObjectSerializer>().As<IObjectSerializer>();
            builder.RegisterInstance(settingsReader).As<IAppSettingsReader>();
            builder.RegisterType<AuthorizationPassThroughClient>()
                   .As<IMicroserviceHttpClient>();
        }

        private static ILogLayout CreateLogLayout(IAppSettingsReader settingsReader)
        {
            var version = typeof(GuestServiceBootstrapper).Assembly.GetName().Version.ToString();

            var logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            var localIpHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            var messageContent = logLayout.Get<LogLayoutMetadata>();
            messageContent.LocalIP = localIpHostEntry.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = settingsReader.GetValue<string>("ServiceName");
            messageContent.Environment = settingsReader.GetValue<string>("Environment");
            messageContent.Facility = settingsReader.GetValue<string>("Facility");
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;

            logLayout.Update(messageContent);

            return logLayout;
        }
    }
}