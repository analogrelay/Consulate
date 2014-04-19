using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Owin;
using Microsoft.Owin.Diagnostics;
using Owin;

[assembly: OwinStartup(typeof(Consulate.Startup))]

namespace Consulate
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var url = "http://" + WebRole.GetIP("Consul.SerfLan").ToString() + ":" + WebRole.GetPort("Consul.Http").ToString();
            
            app.UseErrorPage(ErrorPageOptions.ShowAll);
            app.UseConsulate(new Uri(url));
        }
    }
}