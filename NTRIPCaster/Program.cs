using System;
using System.IO;
using Newtonsoft.Json;
using NTRIPCaster.Configs;
using NTRIPCaster.Server;

namespace NTRIPCaster {
    class Program {
        static void Main(string[] args) {
            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));

            var server = new NTRIPServer(config);
            server.Start();

            Console.WriteLine("Press 'q' to quit");
            while (true) {
                var pressedKey = Console.ReadKey();
                if (pressedKey.KeyChar == 'q') {
                    break;
                }
            }
            server.Stop();
        }
    }
}
