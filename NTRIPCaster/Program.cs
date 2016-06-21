using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NTRIPCaster.Configs;

namespace NTRIPCaster {
    class Program {
        static void Main(string[] args) {
            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
            CheckConfig(config);

            var server = new NTRIPServer(config);
            server.Start();
            Console.Read();
        }

        static void CheckConfig(Config config) {
            // TODO
        }
    }
}
