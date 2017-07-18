using System;
using Xunit;
using Xunit.Abstractions;

namespace Aqovia.PactProducerVerifier.Sample
{
    public class PactProducerTests
    {
        private readonly Aqovia.PactProducerVerifier.PactProducerTests _pactProducerTests;
        private const int TeamCityMaxBranchLength = 19;
        public PactProducerTests(ITestOutputHelper output)
        {
            _pactProducerTests = new Aqovia.PactProducerVerifier.PactProducerTests(output.WriteLine, ThisAssembly.Git.Branch, TeamCityMaxBranchLength);
        }

        [Fact]
        public void EnsureApiHonoursPactWithConsumers()
        {
            _pactProducerTests.EnsureApiHonoursPactWithConsumers();
        }
    }
}
