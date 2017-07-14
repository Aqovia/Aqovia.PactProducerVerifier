using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json.Linq;
using PactNet;
using PactNet.Infrastructure.Outputters;
using RestSharp;
using RestSharp.Authenticators;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Aqovia.PactProducerVerifier
{
    public class PactProducerTests
    {
        protected static readonly string MasterBranchName = "master";
        protected ConfigurationSettings ConfigurationSettings;
        protected RestClient PactBrokerRestClient;
        private readonly object _startup;
        private readonly MethodInfo _method;
        private const string TeamcityprojectnameAppSettingKey = "TeamCityProjectName";
        private static string ProducerServiceName => ConfigurationManager.AppSettings[TeamcityprojectnameAppSettingKey];
        private static string ProjectName => ConfigurationManager.AppSettings["WebProjectName"];
        private readonly XUnitOutput _output;
        private readonly Uri _serviceUri;
        private readonly string _gitBranchName;
        private readonly int _maxBranchNameLength;

        public PactProducerTests(ITestOutputHelper output, Uri serviceUri, string gitBranchName, int maxBranchNameLength = Int32.MaxValue, string fakeModeConfigSetting = null)
        {
            _output = new XUnitOutput(output);
            _serviceUri = serviceUri;
            _gitBranchName = gitBranchName;
            _maxBranchNameLength = maxBranchNameLength;

            if (fakeModeConfigSetting != null &&
                !bool.Parse(ConfigurationManager.AppSettings[fakeModeConfigSetting]))
            {
                throw new ArgumentException($"Fake mode setting: {fakeModeConfigSetting} is false");
            }

            if (ProducerServiceName == null)
            {
                throw new ArgumentException($"App setting '{TeamcityprojectnameAppSettingKey}' is missing");
            }

            PactBrokerRestClient = SetupRestClient();
            var path = AppDomain.CurrentDomain.BaseDirectory;

            Assembly webAssembly;
            try
            {
                webAssembly = ProjectName != null
                    ? Assembly.LoadFile(Directory.GetFileSystemEntries(path, "*.dll")
                        .Single(name => name.EndsWith($"{ProjectName}.dll")))
                    : Assembly.LoadFile(Directory.GetFileSystemEntries(path, "*.dll")
                        .Single(name => name.EndsWith("Web.dll")));
            }
            catch (Exception e)
            {
                throw new FileNotFoundException($"Can not found any dll with name equal to '{ProjectName}' or ending with 'Web.dll'", e);
            }

            Type type;
            try
            {
                type = webAssembly.GetTypes().Single(_ => _.Name == "Startup");
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Can not find Owin Startup class", e);
            }

            _startup = Activator.CreateInstance(type);
            _method = type.GetMethod("Configuration");
        }

        public virtual void EnsureApiHonoursPactWithConsumers()
        {
            using (WebApp.Start(_serviceUri.AbsoluteUri, builder => _method.Invoke(_startup, new List<object> { builder }.ToArray())))
            {
                var consumers = GetConsumers(PactBrokerRestClient);
                var currentBranchName = GetCurrentBranchName();
                foreach (var consumer in consumers)
                {
                    var pactUrl = GetPactUrl(consumer, currentBranchName);
                    var pact = PactBrokerRestClient.Execute(new RestRequest(pactUrl));
                    if (pact.StatusCode != HttpStatusCode.OK)
                    {
                        _output.WriteLine($"Pact does not exist for branch: {currentBranchName}, using {MasterBranchName} instead");
                        pactUrl = GetPactUrl(consumer, MasterBranchName);
                        pact = PactBrokerRestClient.Execute(new RestRequest(pactUrl));
                        if (pact.StatusCode != HttpStatusCode.OK)
                            continue;
                    }
                    VerifyPactWithConsumer(consumer, pactUrl, _serviceUri.AbsoluteUri);
                }
            }
        }

        protected string GetPactUrl(JToken consumer, string branchName)
        {
            return $"pacts/provider/{ProducerServiceName}/consumer/{consumer}/latest/{branchName}";
        }

        protected IEnumerable<JToken> GetConsumers(IRestClient client)
        {
            IEnumerable<JToken> consumers = new List<JToken>();
            var restRequest = new RestRequest($"pacts/provider/{ProducerServiceName}/latest");
            restRequest.AddHeader("Accept", "");
            var response = client.Execute(restRequest);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                dynamic json = JObject.Parse(client.Execute(restRequest).Content);
                var latestPacts = (JArray)json._links.pacts;
                consumers = latestPacts.Select(s => s.SelectToken("name"));
            }
            return consumers;
        }

        protected RestClient SetupRestClient()
        {
            var client = new RestClient
            {
                Authenticator = new HttpBasicAuthenticator(ConfigurationManager.AppSettings["PactBrokerUsername"], ConfigurationManager.AppSettings["PactBrokerPassword"]),
                BaseUrl = new Uri(ConfigurationManager.AppSettings["PactBrokerUri"]),
            };
            return client;
        }
        protected string GetCurrentBranchName()
        {
            var componentBranch = Environment.GetEnvironmentVariable("ComponentBranch");

            _output.WriteLine($"GitBranchName = {_gitBranchName}");
            _output.WriteLine($"Environment Variable 'ComponentBranch' = {componentBranch}");
            
            var branchName = _gitBranchName;
            branchName = string.IsNullOrEmpty(branchName) ? componentBranch : branchName;
            branchName = string.IsNullOrEmpty(branchName) ? MasterBranchName : branchName;

            branchName = branchName?.TrimStart('-')?.Length > _maxBranchNameLength ? 
                 branchName.Substring(0, _maxBranchNameLength)
                : branchName;

            _output.WriteLine($"Calculated BranchName = {branchName}");

            return branchName;
        }

        protected void VerifyPactWithConsumer(JToken consumer, string pactUrl, string serviceUri)
        {
            //we need to instantiate one pact verifier for each consumer

            var config = new PactVerifierConfig
            {
                Outputters = new List<IOutput> 
                {
                    _output
                }
            };

            var pactVerifierOrderService = new PactVerifier(config);
            var serviceProvider = pactVerifierOrderService.ServiceProvider(ProducerServiceName, serviceUri);
            serviceProvider.HonoursPactWith(consumer.ToString());
            var pactUriOrderService = new Uri(new Uri(ConfigurationManager.AppSettings["PactBrokerUri"]), pactUrl);

            PactUriOptions pactUriOptions = null;
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["PactBrokerUsername"]))
                pactUriOptions = new PactUriOptions(ConfigurationManager.AppSettings["PactBrokerUsername"], ConfigurationManager.AppSettings["PactBrokerPassword"]);

            serviceProvider.PactUri(pactUriOrderService.AbsoluteUri, pactUriOptions);
            serviceProvider.Verify();
        }
    }

    public class XUnitOutput : IOutput
    {
        private readonly ITestOutputHelper _output;

        public XUnitOutput(ITestOutputHelper output)
        {
            _output = output;
        }

        public void WriteLine(string line)
        {
            _output.WriteLine(line);
        }
    }
}