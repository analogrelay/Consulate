using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin;

namespace Consulate
{
    public class ConsulateMiddleware : OwinMiddleware
    {
        public ConsulateMiddleware(OwinMiddleware next) : base(next) { }

        public override async Task Invoke(IOwinContext context)
        {
            if (!context.Request.Path.HasValue)
            {
                await context.Response.WriteAsync(
            }
        }
    }
}

namespace Owin
{
    public static class ConsulateMiddlewareExtensions
    {
        public static void UseConsulate(this IAppBuilder self)
        {
            self.Use(typeof(Consulate.ConsulateMiddleware));
        }
    }
}