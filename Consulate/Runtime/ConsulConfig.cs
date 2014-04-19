using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Newtonsoft.Json;

namespace Consulate.Runtime
{
    public class ConsulConfig
    {
        public IPAddress BindAddr { get; set; }
        public IPAddress ClientAddr { get; set; }
        public ConsulPorts Ports { get; set; }
        public string DataDir { get; set; }
        public string NodeName { get; set; }

        public string RpcAddr
        {
            get { return ClientAddr.ToString() + ":" + Ports.Rpc.ToString(); }
        }

        public Uri HttpApiUri
        {
            get { return new Uri("http://" + ClientAddr.ToString() + ":" + Ports.Http.ToString()); }
        }

        public ConsulConfig()
        {
            Ports = new ConsulPorts();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(new
            {
                node_name = NodeName,
                bind_addr = BindAddr == null ? null : BindAddr.ToString(),
                client_addr = ClientAddr == null ? null : ClientAddr.ToString(),
                ports = new
                {
                    dns = Ports.Dns.HasValue ? Ports.Dns.Value : -1,
                    http = Ports.Http.HasValue ? Ports.Http.Value : -1,
                    rpc = Ports.Rpc,
                    serf_lan = Ports.SerfLan,
                    serf_wan = Ports.SerfWan,
                    server = Ports.Server
                },
                data_dir = DataDir
            });
        }
    }
}