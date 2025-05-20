using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aqovia.PactProducerVerifier
{
    public class DefaultProviderStateMiddleware : BaseProviderStateMiddleware
    {
        public DefaultProviderStateMiddleware(Func<IDictionary<string, object>, Task> next) : base(next)
        {
        }

        protected override IDictionary<string, Action> ProviderStates => new Dictionary<string, Action>();
    }
}
