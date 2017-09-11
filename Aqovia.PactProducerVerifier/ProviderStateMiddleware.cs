using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;

namespace Aqovia.PactProducerVerifier
{
    public abstract class BaseProviderStateMiddleware
    {
        private readonly Func<IDictionary<string, object>, Task> _mNext;
        protected abstract IDictionary<string, Action> ProviderStates { get; }

        protected BaseProviderStateMiddleware(Func<IDictionary<string, object>, Task> next)
        {
            _mNext = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            IOwinContext context = new OwinContext(environment);

            if (context.Request.Path.Value == "/provider-states")
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                if (context.Request.Method == HttpMethod.Post.ToString() &&
                    context.Request.Body != null)
                {
                    string jsonRequestBody;
                    using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                    {
                        jsonRequestBody = reader.ReadToEnd();
                    }

                    var providerState = JsonConvert.DeserializeObject<ProviderState>(jsonRequestBody);

                    //A null or empty provider state key must be handled
                    if (!string.IsNullOrEmpty(providerState?.State))
                    {
                        ProviderStates[providerState.State].Invoke();
                    }

                    await context.Response.WriteAsync(string.Empty);
                }
            }
            else
            {
                await _mNext.Invoke(environment);
            }
        }
    }

    public class ProviderState
    {
        public string State { get; set; }
        public string Consumer { get; set; }
    }
}