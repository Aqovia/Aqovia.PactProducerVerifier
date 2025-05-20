# Aqovia.PactProducerVerifier

A utility for verifying producer code against all consumers on the Pact Broker.
It calls the pact broker and retrieves all pacts where it is a producer (using TeamCityProjectName config setting)
and allows for branching using either the passed in Git Branch Name or the teamcity environment variable "ComponentBranch"

[![Build status](https://ci.appveyor.com/api/projects/status/jltbacetwhyu9t2x/branch/master?svg=true)](https://ci.appveyor.com/project/aqovia/aqovia-pactproducerverifier/branch/master)
[![NuGet Badge](https://buildstats.info/nuget/aqovia.pactproducerverifier)](https://www.nuget.org/packages/aqovia.pactproducerverifier)/)

## Assumptions

Build server is TeamCity, as it uses the environment variable "ComponentBranch" to determine the branch of the code on the build server

## Getting started

This uses the beta version of PactNet, and Team City.

* Install the latest beta version of the [PactNet](https://github.com/pact-foundation/pact-net) package (at time of writing 5.0.0)
* Install this package Aqovia.PactProducerVerifier
* Install a test framework such as XUnit
* Install GitInfo if you require to work out the git branch name locally

* Add the test (example using XUnit)
```
    public class PactProducerTests
    {
        private readonly Aqovia.PactProducerVerifier.PactProducerTests _pactProducerTests;
        private const int TeamCityMaxBranchLength = 19;
        public PactProducerTests(ITestOutputHelper output)
        {
			var configuration = new ProducerVerifierConfiguration
            {
                ProviderName = "<YOUR NAME OF THE PROVIDER (E.G THE PROJECT NAME)",
                PactBrokerUri = "<YOUR PACT BROKER URL>",
				PactBrokerToken = "<YOUR PACT BROKER TOKEN , mandatory>",
                ProjectName = "WEB API PROJECT YOU ARE TESTING (IF DOESN'T END IN Web.dll)')"
            };

			 _pactProducerTests = new PactProducerTests(configuration, output.WriteLine, ThisAssembly.Git.Branch, null, TeamCityMaxBranchLength);

			 // Or if you have given statements, set up state in the provider to match the given statements

			 _pactProducerTests = new PactProducerTests(configuration, output.WriteLine, ThisAssembly.Git.Branch, builder =>
            {
                builder.UseMiddleware(typeof(TestStateProvider));

            }, TeamCityMaxBranchLength);
        }


        [Fact]
        public void EnsureApiHonoursPactWithConsumers()
        {
           _pactProducerTests.EnsureApiHonoursPactWithConsumers();
        }
    }

	public class TestStateProvider : BaseProviderStateMiddleware
	{
	        protected override IDictionary<string, Action> ProviderStates =>
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
			// populate with strings that match given statements with actions that set up the provider
	}
```
The PactProducerTests constructor takes in 3 parameters:
* Configuration settings
* An Action<string> - this is used so the output of the pact test is outputted to the test results (in XUnit in this example)
* The branch this code is in. This is used locally, but if running on the build server it uses the environment variable "ComponentBranch"
* Callback for installing middleware in the AspNET pipeline. i.e. a custom state provider
* The maximum branch name length (optional)

## Sample
A sample is included in the source - in the samples folder. To use this:
* Update the PactBrokerUri configuration setting to the uri of the broker your using.
* Remove the Skip parameter in the [Fact] attribute


## Upgrading from 1.x

As of version 2.x this library upgrades PactNet from version 2.x to 5.x. This is a breaking change as there are significant syntax changes between version 2 and 5, please see the PactNet documentation for [upgrading from < 3 to version 4](https://github.com/pact-foundation/pact-net/blob/master/docs/upgrading-to-4.md) and [upgrading from 4 to 5](https://github.com/pact-foundation/pact-net/blob/master/docs/upgrading-to-5.md) for guidance.