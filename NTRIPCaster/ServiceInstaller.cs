using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

namespace NTRIPCaster {
    [RunInstaller(true)]
    public partial class ServiceInstaller : System.Configuration.Install.Installer {
        internal static string SERVICE_NAME = "NTRIPCaster";

        public string ServiceName
        {
            get { return serviceInstaller1.DisplayName; }
            set
            {
                serviceInstaller1.DisplayName = value;
                serviceInstaller1.ServiceName = value;
            }
        }

        public ServiceInstaller() {
            InitializeComponent();

            ServiceName = SERVICE_NAME;

            serviceInstaller1.Description = "Simple NTRIP Caster";
            serviceInstaller1.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            serviceProcessInstaller1.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
        }
    }
}
