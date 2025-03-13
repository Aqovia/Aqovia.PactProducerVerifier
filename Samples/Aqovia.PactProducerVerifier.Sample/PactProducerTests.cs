using System.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Aqovia.PactProducerVerifier.Sample
{
    public class PactProducerTests
    {
        private readonly Aqovia.PactProducerVerifier.PactProducerTests _pactProducerTests;
        private const int maxBranchNameLength = 19;
        public PactProducerTests(ITestOutputHelper output)
        {
            var configuration = new ProducerVerifierConfiguration
            {
                ProviderName = ConfigurationManager.AppSettings["ProviderName"],
                ProjectName = ConfigurationManager.AppSettings["ProjectName"],
                PactBrokerUri = ConfigurationManager.AppSettings["PactBrokerUri"],
                PactBrokerToken = ConfigurationManager.AppSettings["PactBrokerToken"]
            };
            _pactProducerTests = new Aqovia.PactProducerVerifier.PactProducerTests(configuration, output.WriteLine, ThisAssembly.Git.Branch, null, maxBranchNameLength);
        }

        [Fact]
        public void EnsureApiHonoursPactWithConsumers()
        {
            _pactProducerTests.EnsureApiHonoursPactWithConsumers();
        }
    }
}
