using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json.Linq;
using Owin;
using PactNet;
using PactNet.Infrastructure.Outputters;
using RestSharp;
using RestSharp.Authenticators;

namespace Aqovia.PactProducerVerifier
{
    public class PactProducerTests
    {
        private const string MasterBranchName = "master";
        private const string BaseServiceUri = "http://localhost";

        private readonly RestClient _pactBrokerRestClient;
        private readonly object _startup;
        private readonly MethodInfo _method;
        private readonly ActionOutput _output;
        private readonly ProducerVerifierConfiguration _configuration;
        private readonly string _gitBranchName;
        private readonly Action<IAppBuilder> _onWebAppStarting;
        private readonly int _maxBranchNameLength;
        private readonly AppDomainHelper _appDomainHelper;

        public PactProducerTests(ProducerVerifierConfiguration configuration, Action<string> output, string gitBranchName, Action<IAppBuilder> onWebAppStarting = null, int maxBranchNameLength = int.MaxValue)
        {
            _output = new ActionOutput(output);
            _configuration = configuration;
            _gitBranchName = gitBranchName;
            _onWebAppStarting = onWebAppStarting;
            _maxBranchNameLength = maxBranchNameLength;

            if (string.IsNullOrEmpty(configuration.ProviderName))
            {
                throw new ArgumentException($"App setting '{nameof(configuration.ProviderName)}' is missing or not set");
            }

            if (string.IsNullOrEmpty(configuration.PactBrokerUri))
            {
                throw new ArgumentException($"App setting '{nameof(configuration.PactBrokerUri)}' is missing or not set");
            }

            _pactBrokerRestClient = SetupRestClient();
            var path = AppDomain.CurrentDomain.BaseDirectory;

            Assembly webAssembly;
            _appDomainHelper = new AppDomainHelper();
            try
            {
                webAssembly = _appDomainHelper.LoadAssembly(configuration.ProjectName != null
                    ? new FileInfo(Directory.GetFileSystemEntries(path, "*.dll")
                        .Single(name => name.EndsWith($"{configuration.ProjectName}.dll")))
                    : new FileInfo(Directory.GetFileSystemEntries(path, "*.dll")
                        .Single(name => name.EndsWith("Web.dll"))));
            }
            catch (Exception e)
            {
                throw new FileNotFoundException($"Can not found any dll with name equal to '{nameof(configuration.ProjectName)}' or ending with 'Web.dll'", e);
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

        public void EnsureApiHonoursPactWithConsumers()
        {
            const int maxRetries = 5;
            var random = new Random();
            var uriBuilder = new UriBuilder(BaseServiceUri);
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    uriBuilder.Port = random.Next(10000, 20000);
                    EnsureApiHonoursPactWithConsumers(uriBuilder.Uri);
                    break;
                }
                catch (HttpListenerException ex)
                {
                    _output.WriteLine($"Service Uri: {uriBuilder.Uri.AbsoluteUri} failed with: {ex.Message}");
                    if(i < maxRetries)
                        _output.WriteLine("will retry ...");
                }
            }
            //unload the newly created child appdomain
            _appDomainHelper.Dispose();
        }

        private void EnsureApiHonoursPactWithConsumers(Uri uri)
        {
            using (WebApp.Start(uri.AbsoluteUri, builder =>
            {
                _onWebAppStarting?.Invoke(builder);

                _method.Invoke(_startup, new List<object> { builder }.ToArray());
            }))
            {
                var consumers = GetConsumers(_pactBrokerRestClient);
                var currentBranchName = GetCurrentBranchName();
                foreach (var consumer in consumers)
                {
                    var pactUrl = GetPactUrl(consumer, currentBranchName);
                    var pact = _pactBrokerRestClient.Execute(new RestRequest(pactUrl));
                    if (pact.StatusCode != HttpStatusCode.OK)
                    {
                        _output.WriteLine($"Pact does not exist for branch: {currentBranchName}, using {MasterBranchName} instead");
                        pactUrl = GetPactUrl(consumer, MasterBranchName);
                        pact = _pactBrokerRestClient.Execute(new RestRequest(pactUrl));
                        if (pact.StatusCode != HttpStatusCode.OK)
                            continue;
                    }
                    VerifyPactWithConsumer(consumer, pactUrl, uri.AbsoluteUri);
                }
            }
        }

        private string GetPactUrl(JToken consumer, string branchName)
        {
            return $"pacts/provider/{_configuration.ProviderName}/consumer/{consumer}/latest/{branchName}";
        }

        private IEnumerable<JToken> GetConsumers(IRestClient client)
        {
            IEnumerable<JToken> consumers = new List<JToken>();
            var restRequest = new RestRequest($"pacts/provider/{_configuration.ProviderName}/latest");
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

        private RestClient SetupRestClient()
        {
            var client = new RestClient
            {
                Authenticator = new HttpBasicAuthenticator(_configuration.PactBrokerUsername, _configuration.PactBrokerPassword),
                BaseUrl = new Uri(_configuration.PactBrokerUri),
            };
            return client;
        }
        private string GetCurrentBranchName()
        {
            var componentBranch = Environment.GetEnvironmentVariable("ComponentBranch");

            _output.WriteLine($"GitBranchName = {_gitBranchName}");
            _output.WriteLine($"Environment Variable 'ComponentBranch' = {componentBranch}");
            
            var branchName = _gitBranchName;
            branchName = string.IsNullOrEmpty(componentBranch) ? branchName : componentBranch;
            branchName = string.IsNullOrEmpty(branchName) ? MasterBranchName : branchName;

            branchName = branchName?.TrimStart('-').Length > _maxBranchNameLength ? 
                 branchName.TrimStart('-').Substring(0, _maxBranchNameLength)
                : branchName.TrimStart('-');

            _output.WriteLine($"Calculated BranchName = {branchName}");

            return branchName;
        }

        private void VerifyPactWithConsumer(JToken consumer, string pactUrl, string serviceUri)
        {
            //we need to instantiate one pact verifier for each consumer

            var config = new PactVerifierConfig
            {
                Outputters = new List<IOutput> 
                {
                    _output
                }
            };

            PactUriOptions pactUriOptions = null;
            if (!string.IsNullOrEmpty(_configuration.PactBrokerUsername))
                pactUriOptions = new PactUriOptions(_configuration.PactBrokerUsername, _configuration.PactBrokerPassword);

            var pactUri = new Uri(new Uri(_configuration.PactBrokerUri), pactUrl);
            var pactVerifier = new PactVerifier(config);

            pactVerifier
                .ProviderState($"{serviceUri}/provider-states")
                .ServiceProvider(_configuration.ProviderName, serviceUri)
                .HonoursPactWith(consumer.ToString())
                .PactUri(pactUri.AbsoluteUri, pactUriOptions)
                .Verify();
        }
        private class ActionOutput : IOutput
        {
            private readonly Action<string> _output;

            public ActionOutput(Action<string> output)
            {
                _output = output;
            }

            public void WriteLine(string line)
            {
                _output.Invoke(line);
            }
        }
    }


}
