using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRIPCaster.Configs {

    public class User {
        public string Name { get; set; }
        public string Password { get; set; }
        public IList<string> Mountpoints { get; set; }

        public User() {
            this.Mountpoints = new List<string>();
        }
    }
}
