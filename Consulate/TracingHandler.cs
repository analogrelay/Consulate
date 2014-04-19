using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Consulate
{
    public class TracingHandler : DelegatingHandler
    {
        private static readonly Logger Log = LogManager.GetLogger(typeof(TracingHandler).FullName);

        public TracingHandler() : base() { }
        public TracingHandler(HttpMessageHandler next) : base(next) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            HttpResponseMessage result = null;
            try
            {
                Log.Info("{0} {1}", request.Method, request.RequestUri.ToString());
                result = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error("{0} {1}: {2} - {3}", request.Method, request.RequestUri.ToString(), ex.GetType().FullName, ex.Message);
            }
            if (result != null)
            {
                Log.Info("{0} {1}", result.StatusCode, request.RequestUri.ToString());
            }
            return result;
        }
    }
}
