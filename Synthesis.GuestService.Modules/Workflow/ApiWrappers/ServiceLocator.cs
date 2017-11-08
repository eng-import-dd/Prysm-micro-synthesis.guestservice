using System;
using Synthesis.Configuration;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class ServiceLocator : IServiceLocator
    {
        public ServiceLocator(IAppSettingsReader reader, ILoggerFactory loggerFactory)
        {
            try
            {
                ParticipantUrl = reader.GetValue<string>("ParticipantUrl");
                ProjectUrl = reader.GetValue<string>("ProjectUrl");
                SettingsUrl = reader.GetValue<string>("SettingsUrl");
                UserUrl = reader.GetValue<string>("UserUrl");
            }
            catch (Exception e)
            {
                loggerFactory.GetLogger(this).Error("Error retriving url from config", e);
            }
        }

        /// <inheritdoc />
        public string ParticipantUrl { get; }

        /// <inheritdoc />
        public string ProjectUrl { get; }

        /// <inheritdoc />
        public string SettingsUrl { get; }

        /// <inheritdoc />
        public string UserUrl { get; }
    }
}