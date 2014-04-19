using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Consulate.Runtime;
using Microsoft.Owin;
using Newtonsoft.Json;

namespace Consulate
{
    public class ConsulateMiddleware : OwinMiddleware
    {
        private Uri _consulBase;
        private HttpClient _consulClient;

        public ConsulateMiddleware(OwinMiddleware next, Uri consulBase) : base(next) {
            _consulBase = consulBase;
            _consulClient = new HttpClient(new TracingHandler(new HttpClientHandler()))
            {
                BaseAddress = consulBase
            };
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (!context.Request.Path.HasValue || context.Request.Path.Value == "/")
            {
                await context.Response.WriteAsync(
                    await JsonConvert.SerializeObjectAsync(
                        new
                        {
                            consulApi = "/api"
                        }));
            }
            else if (context.Request.Path.Value.StartsWith("/api"))
            {
                // Strip the path
                string remainingPath = context.Request.Path.Value.Substring(4);

                // Translate the request
                HttpRequestMessage message = new HttpRequestMessage(new HttpMethod(context.Request.Method), remainingPath);
                foreach (var header in context.Request.Headers)
                {
                    message.Headers.Add(header.Key, header.Value);
                }
                message.Headers.Add("X-Forwarded-By", "Consulate");
                var response = await _consulClient.SendAsync(message);

                // Apply the response
                context.Response.StatusCode = (int)response.StatusCode;
                foreach (var header in response.Headers)
                {
                    context.Response.Headers.Add(header.Key, header.Value.ToArray());
                }
                context.Response.Headers.Add("X-Forwarded-For", new [] { "Consulate" });
                context.Response.ReasonPhrase = response.ReasonPhrase;
                await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
            }
        }
    }
}

namespace Owin
{
    public static class ConsulateMiddlewareExtensions
    {
        public static void UseConsulate(this IAppBuilder self, Uri consulBase)
        {
            self.Use(typeof(Consulate.ConsulateMiddleware), consulBase);
        }
    }
}