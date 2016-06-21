using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NTRIPCaster.Configs {

    public class Source {
        public string Mountpoint { get; set; }
        public string Identifier { get; set; }
        public string Format { get; set; }
        public Carrier Carrier { get; set; }
        public string NavSystem { get; set; }
        public string Network { get; set; }
        public string Country { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool AuthRequired { get; set; }
        public string Password { get; set; }
    }

    public enum Carrier {
        No = 0,
        L1 = 1,
        L1L2 = 2
    }
}
