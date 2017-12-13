using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aqovia.PactProducerVerifier
{
    public class ProducerVerifierConfiguration
    {
        public string ProviderName { get; set; }
        public string ProjectName { get; set; }
        public string PactBrokerUsername { get; set; }
        public string PactBrokerPassword { get; set; }
        public string PactBrokerUri { get; set; }
    }
}
