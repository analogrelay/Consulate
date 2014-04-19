using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Formatting;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Consulate.Api;
using Consulate.Runtime;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Consulate
{
    public class WebRole : RoleEntryPoint
    {
        private static WebRole _current;
        public static WebRole Current { get { return _current; } }

        private static readonly Logger Log = LogManager.GetLogger(typeof(WebRole).FullName);

        private static readonly Regex IdZeroMatcher = new Regex(".*_IN_0");
        private ConsulAgent _agent;

        public ConsulAgent Agent { get { return _agent; } }

        public override bool OnStart()
        {
            return OnStartCore().Result;
        }

        private async Task<bool> OnStartCore()
        {
            _current = this;

            // Set up logging
            LoggingConfiguration logConfig = new LoggingConfiguration();
            var fileTarget = new FileTarget()
            {
                Layout = "[${logger}](${date}) ${message}",
                FileName = Path.Combine(RoleEnvironment.GetLocalResource("ConsulLogs").RootPath, "Consulate.log")
            };
            var traceTarget = new TraceTarget()
            {
                Layout = "[${logger}] ${message}"
            };
            logConfig.AddTarget("file", fileTarget);
            logConfig.AddTarget("trace", traceTarget);

            logConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, fileTarget));
            logConfig.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, traceTarget));
            LogManager.Configuration = logConfig;

            // Create config
            var config = new ConsulConfig()
            {
                NodeName = RoleEnvironment.CurrentRoleInstance.Id,
                DataDir = RoleEnvironment.GetLocalResource("ConsulData").RootPath,
                BindAddr = GetIP("Consul.Rpc"),
                ClientAddr = GetIP("Consul.Rpc"),
                Ports = new ConsulPorts()
                {
                    Dns = GetPort("Consul.Dns"),
                    Http = GetPort("Consul.Http"),
                    Rpc = GetPort("Consul.Rpc"),
                    SerfLan = GetPort("Consul.SerfLan"),
                    SerfWan = GetPort("Consul.SerfWan"),
                    Server = GetPort("Consul.Server")
                }
            };

            var clients = GetClients();

            // Step 1 - Poll for an existing cluster
            Log.Info("Searching for cluster...");
            var existingCluster = await FindExistingCluster(clients);

            if (!existingCluster.Any())
            {
                Log.Info("No cluster found, attempting to bootstrap one!");
                _agent = await Bootstrap(clients, config);
            }
            else
            {
                Log.Info("Found a cluster! Joining it");
                _agent = await Join(config, existingCluster);
            }
            return true;
        }

        private IEnumerable<ConsulClient> GetClients()
        {
            var myPort = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["Consul.Http"].IPEndpoint.Port;
            return RoleEnvironment.CurrentRoleInstance.Role.Instances.Select(ri => new ConsulClient(GetUrl(ri.InstanceEndpoints["Consul.SerfLan"].IPEndpoint.Address, myPort)));
        }

        private Uri GetUrl(IPAddress address, int port)
        {
            return new Uri("http://" + address.ToString() + ":" + port.ToString());
        }

        private async Task<IEnumerable<string>> FindExistingCluster(IEnumerable<ConsulClient> clients)
        {
            // Try all our peers' HTTP APIs to see if we can join a cluster
            var tasks = clients.Select(c => c.Peers()).ToList();

            IEnumerable<string> results = null;
            while (tasks.Count > 0)
            {
                var t = await Task.WhenAny(tasks);
                tasks.Remove(t);
                try
                {
                    return await t;
                }
                catch (Exception)
                {
                    // No result from this node... Keep waiting
                }
            }
            return Enumerable.Empty<string>(); // No results!
        }

        public override void OnStop()
        {
            _agent.Shutdown();
            _agent.WaitForExit();
            base.OnStop();
        }

        public static int GetPort(string ep)
        {
            return RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[ep].IPEndpoint.Port;
        }

        public static IPAddress GetIP(string ep)
        {
            return RoleEnvironment.CurrentRoleInstance.InstanceEndpoints[ep].IPEndpoint.Address;
        }

        private Task<ConsulAgent> Join(ConsulConfig config, IEnumerable<string> existingCluster)
        {
            return Task.FromResult(ConsulAgent.LaunchServer(
                bootstrap: false,
                config: config,
                join: RoleEnvironment
                    .CurrentRoleInstance
                    .Role
                    .Instances
                    .Where(i => i != RoleEnvironment.CurrentRoleInstance)
                    .Select(i => i.InstanceEndpoints["Consul.SerfLan"].IPEndpoint.Address)));
        }

        private async Task<ConsulAgent> Bootstrap(IEnumerable<ConsulClient> clients, ConsulConfig config)
        {
            // Try to lock the bootstrapper blob
            var acct = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageAccount"));
            var container = acct.CreateCloudBlobClient().GetContainerReference("consulate");
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlockBlobReference("bootstrap.lock");
            try
            {
                await blob.UploadTextAsync("Bootstrap Lock. Only used for leasing", null, AccessCondition.GenerateIfNoneMatchCondition("*"), new BlobRequestOptions(), new OperationContext());
            }
            catch (StorageException ex)
            {
                if (ex.RequestInformation == null || ex.RequestInformation.HttpStatusCode != 409)
                {
                    // Expected a conflict, but this was not a conflict :(
                    throw;
                }
            }

            // Attempt to lease the blob
            string leaseId = null;
            try
            {
                bool leased = false;
                while (!leased)
                {
                    try
                    {
                        Log.Info("Acquiring one-minute bootstrapper lease.");
                        leaseId = await blob.AcquireLeaseAsync(TimeSpan.FromMinutes(1), null);
                        leased = true;
                    }
                    catch (StorageException ex)
                    {
                        if (ex.RequestInformation == null || ex.RequestInformation.ExtendedErrorInformation == null || ex.RequestInformation.ExtendedErrorInformation.ErrorCode != BlobErrorCodeStrings.LeaseAlreadyPresent)
                        {
                            throw;
                        }
                        else
                        {
                            // Already leased
                            leased = false;
                        }
                    }

                    // We may or may not have a lease, but either way, check for clusters again
                    Log.Info("Checking for clusters again");
                    var cluster = await FindExistingCluster(clients);
                    if (cluster.Any())
                    {
                        Log.Info("Found a cluster!");
                        if (leased)
                        {
                            Log.Info("Releasing Bootstrap lease.");
                            await blob.ReleaseLeaseAsync(AccessCondition.GenerateLeaseCondition(leaseId));
                        }

                        // Join the existing cluster
                        return await Join(config, cluster);
                    }
                }

                Log.Info("Bootstreap lease acquired, launching agent in bootstrap mode");
                var agent = ConsulAgent.LaunchServer(bootstrap: true, config: config);
                var client = new ConsulClient(config.HttpApiUri);
                IList<string> nodes = null;
                int quorum = (RoleEnvironment.CurrentRoleInstance.Role.Instances.Count / 2) + 1;
                Log.Info("Waiting for quorum of {0} nodes to join...", quorum);
                while (nodes == null || nodes.Count < quorum)
                {
                    Log.Info("Checking how many nodes have connected", quorum);
                    nodes = (await client.Peers()).ToList();
                    if (nodes.Count < quorum)
                    {
                        Log.Info("{0} nodes have joined, no quorum yet. Sleeping for one second", nodes.Count);
                        await Task.Delay(1000);
                    }
                }
                Log.Info("Quorum reached! Leaving and rejoining");
                agent.Shutdown();
                agent.WaitForExit();
                Log.Info("Agent has shut down, rejoining as non-bootstrapper...");
                return ConsulAgent.LaunchServer(bootstrap: false, config: config, join: nodes.Select(s => IPAddress.Parse(s.Substring(0, s.IndexOf(":")))));
            }
            finally
            {
                if (leaseId != null)
                {
                    try
                    {
                        Log.Warn("Releasing lease in finally block!");
                        blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
                    }
                    catch (Exception) { }
                }
            }
        }
    }
}
