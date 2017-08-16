using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using Microsoft.ServiceFabric.Services.Runtime;
using Synthesis.Owin.Host;
using Synthesis.ApplicationInsights;

namespace Synthesis.GuestService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                var version = typeof(Program).Assembly.GetName().Version.ToString();
                var deploymentName = ConfigurationManager.AppSettings["AI.DeploymentName"];

                var instrumentationKey = ConfigurationManager.AppSettings["AI.InstrumentationKey"];
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    // Creating this initializes the Telemetry Context
                    MicroServiceTelemetryInitializer.Initialize(version, deploymentName, instrumentationKey);
                }

                try
                {
                  // The ServiceManifest.XML file defines one or more service type names.
                  // Registering a service maps a service type name to a .NET type.
                  // When Service Fabric creates an instance of this service type,
                  // an instance of the class is created in this host process.
                  ServiceRuntime.RegisterServiceAsync("GuestServiceType",
                      context => new GuestService(context)).GetAwaiter().GetResult();

                  ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(GuestService).Name);
                  // Prevents this host process from terminating so services keeps running.
                  Thread.Sleep(Timeout.Infinite);
                }
                catch (Exception e)
                {
                    ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                    throw;
                }
            }
            else
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Command line parameters must be provided for the service name, service base URI, and MacroViewer registration URI.");
                }

                ServiceFabricOwinHost serviceFabricOwinHost = new ServiceFabricOwinHost();
                serviceFabricOwinHost.Start(typeof(Startup), args[0], args[1], args[2]);
            }
        }
    }
}
