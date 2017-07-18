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

* Install the latest beta version 2.0.X-beta of PactNet package (use allow Pre-release option)
* Install this package Aqovia.PactProducerVerifier
* Install a test framework such as XUnit
* Install GitInfo if you require to work out the git branch name locally 
* Add the following configuration to your app.config file
```
  <appSettings>
    <add key="PactBrokerUri" value="<YOUR PACT BROKER URL>" />
    <add key="PactBrokerUsername" value="<YOUR PACT BROKER USERNAME OR BLANK>" />
    <add key="PactBrokerPassword" value="<YOUR PACT BROKER PASSWORD OR BLANK>" />
    <add key="TeamCityProjectName" value="<YOUR NAME OF THE PROJECT (PRODUCER)" />
  </appSettings>
```
* If your web api project your testing doesn't end in Web.dll, add the configuration setting:
```
    <add key="WebProjectName" value="<YOUR WEB API PROJECT NAME" />
```
* Add the test (example using XUnit)
```
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
```
The PactProducerTests constructor takes in 3 parameters:
* An Action<string> - this is used so the output of the pact test is outputted to the test results (in XUnit in this example)
* The branch this code is in. This is used locally, but if running on the build server it uses the environment variable "ComponentBranch"
* The maximum branch name length (optional)

## Sample
A sample is included in the source - in the samples folder. To use this:
* Update the PactBrokerUri configuration setting to the uri of the broker your using.
* Remove the Skip parameter in the [Fact] attribute

