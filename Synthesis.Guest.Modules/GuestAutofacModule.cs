using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Synthesis.Cache;
using Synthesis.Configuration;
using Synthesis.DocumentStorage;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.ExpirationNotifierService.InternalApi.Api;
using Synthesis.ExpirationNotifierService.InternalApi.Services;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Email;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.InternalApi.Api;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Utilities;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Http.Microservice;
using Synthesis.Microservice.Health;
using Synthesis.Nancy.Autofac.Module.Configuration;
using Synthesis.Nancy.Autofac.Module.DocumentDb;
using Synthesis.Nancy.Autofac.Module.Microservice;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.SettingService.InternalApi.Api;
using Module = Autofac.Module;

namespace Synthesis.GuestService
{
    public class GuestAutofacModule : Module
    {
        private const string ServiceToServiceProjectAccessApiKey = "ServiceToServiceProjectAccessApiKey";
        private const string ServiceToServiceProjectApiKey = "ServiceToServiceProjectApiKey";
        private const string ServiceToServiceSettingApiKey = "ServiceToServiceSettingApiKey";

        protected override void Load(ContainerBuilder builder)
        {
            var dbConfigDictionary = new Dictionary<string, string>
            {
                {DocumentDbAutofacModule.AuthKey, "Guest.DocumentDb.AuthKey"},
                {DocumentDbAutofacModule.Endpoint, "Guest.DocumentDb.Endpoint"},
                {DocumentDbAutofacModule.DatabaseName, "Guest.DocumentDb.DatabaseName"},
                {DocumentDbAutofacModule.RuThroughput, "Guest.DocumentDb.RuThroughput"}
            };

            builder.RegisterModule<ConfigurationAutofacModule>();
            builder.RegisterModule(new MicroserviceAutofacModule(dbConfigDictionary, 
                ServiceInformation.ServiceName, 
                ServiceInformation.ServiceNameShort,
                Assembly.GetAssembly(GetType())));
            
            // Event Subscriber
            builder
                .RegisterType<EventSubscriber>()
                .AsSelf()
                .AutoActivate();
            
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
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);
            builder.RegisterType<RepositoryHealthReporter<GuestSession>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);

            builder.RegisterType<EmailApi>().As<IEmailApi>();
            builder.RegisterType<EmailSendingService>().As<IEmailSendingService>();
            builder.RegisterType<ExpirationNotifierApi>().As<IExpirationNotifierApi>();

            // TODO: Replace IGuestApi implementation with an internal version that calls controllers.
            builder.RegisterType<GuestApi>().As<IGuestApi>();        }
    }
}
