using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NTRIPCaster.Configs;
using NTRIPCaster.Server;

namespace NTRIPCaster {

    public class NTRIPService : ServiceBase {
        private NTRIPServer Server;

        public NTRIPService() {
            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json")));
            Server = new NTRIPServer(config);
        }

        protected override void OnStart(string[] args) {
            Server.Start();
        }

        protected override void OnStop() {
            Server.Stop();
        }
    }
}
