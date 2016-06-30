using System.Collections.Generic;

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
