using System;
using System.Collections.Generic;

namespace NTRIPCaster.Configs {

    public class Config {
        public String ServerAddress;
        public int ServerPort;
        public int MaxClients;
        public IList<Source> Sources;
        public IList<User> Users;

        bool CheckConfig() {
            // TODO
            return true;
        }
    }
}
