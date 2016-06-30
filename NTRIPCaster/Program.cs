using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NTRIPCaster.Configs;
using NTRIPCaster.Server;

namespace NTRIPCaster {
    class Program {
        static void Main(string[] args) {
            if (Environment.UserInteractive) {

                if (args.Length > 0 && args[0] == "/install") {
                    InstallAndStart();
                    return;
                }

                var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

                var server = new NTRIPServer(config);
                server.Start();
                Console.Read();
            } else {
                ServiceBase.Run(new NTRIPService());
            }
        }

        #region Installer
        // Copied from RavenDB

        private static void InstallAndStart() {
            if (ServiceIsInstalled()) {
                Console.WriteLine("Service is already installed");
            } else {
                ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                SetRecoveryOptions(ServiceInstaller.SERVICE_NAME);
                var startController = new ServiceController(ServiceInstaller.SERVICE_NAME);
                startController.Start();
            }
        }

        private static bool ServiceIsInstalled() {
            return (ServiceController.GetServices().Count(s => s.ServiceName == ServiceInstaller.SERVICE_NAME) > 0);
        }

        static void SetRecoveryOptions(string serviceName) {
            int exitCode;
            var arguments = string.Format("failure \"{0}\" reset= 500 actions= restart/60000", serviceName);
            using (var process = new Process()) {
                var startInfo = process.StartInfo;
                startInfo.FileName = "sc";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;

                // tell Windows that the service should restart if it fails
                startInfo.Arguments = arguments;

                process.Start();
                process.WaitForExit();

                exitCode = process.ExitCode;

                process.Close();
            }

            if (exitCode != 0)
                throw new InvalidOperationException(
                    "Failed to set the service recovery policy. Command: " + Environment.NewLine + "sc " + arguments + Environment.NewLine + "Exit code: " + exitCode);
        }

        #endregion
    }
}
