using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIPCaster.Configs {

    public class Config {
        public String ServerAddress;
        public int ServerPort;
        public int MaxClients;
        public IList<Source> Sources;
        public IList<User> Users;
    }
}
