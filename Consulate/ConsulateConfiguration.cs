using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Consulate
{
    public class ConsulateConfiguration
    {
        public Version Version { get; private set; }

        public ConsulateConfiguration()
        {
            Version = typeof(ConsulateConfiguration).Assembly.GetName().Version;
        }
    }
}
