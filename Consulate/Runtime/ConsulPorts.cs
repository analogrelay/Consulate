using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Consulate.Runtime
{
    public class ConsulPorts
    {
        public int? Dns { get; set; }
        public int? Http { get; set; }
        public int Rpc { get; set; }
        public int SerfLan { get; set; }
        public int SerfWan { get; set; }
        public int Server { get; set; }

        public ConsulPorts()
        {
            Dns = 8600;
            Http = 8500;
            Rpc = 8400;
            SerfLan = 8301;
            SerfWan = 8302;
            Server = 8300;
        }
    }
}
