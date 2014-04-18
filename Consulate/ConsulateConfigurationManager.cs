using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace Consulate
{
    public class ConsulateConfigurationManager
    {
        private ConsulateConfiguration _config;

        public ConsulateConfiguration Config { get { return _config; } }

        public void AtomicSet(ConsulateConfiguration config)
        {
            Interlocked.Exchange(ref _config, config);
        }
    }
}