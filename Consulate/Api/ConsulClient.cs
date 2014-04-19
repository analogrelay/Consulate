using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace Consulate.Api
{
    public class ConsulClient
    {
        private HttpClient _client;
        private IEnumerable<MediaTypeFormatter> _formatters;

        public ConsulClient(Uri baseAddress)
        {
            _client = new HttpClient(new TracingHandler(new HttpClientHandler()))
            {
                BaseAddress = baseAddress
            };

            var formatter = new JsonMediaTypeFormatter();
            formatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/plain"));
            _formatters = new MediaTypeFormatter[] { formatter };
        }

        public async Task<IEnumerable<string>> Peers()
        {
            var result = await _client.GetAsync("/v1/status/peers");
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadAsAsync<IEnumerable<string>>(_formatters);
        }
    }
}