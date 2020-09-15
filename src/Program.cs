using Microsoft.Azure.Management.ContainerRegistry;
using Microsoft.Azure.Management.ContainerRegistry.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;

using Microsoft.Azure.ContainerRegistry;

using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;
using Microsoft.Rest;
using Microsoft.Azure.ContainerRegistry.Models;

namespace AcrPurge
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                string appSettingsFile = args.Length > 0
                    ? args[0]
                    : Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), "appsettings.json");
                var options = LoadOptions(appSettingsFile);
                options.Validate();

                // Build a container image using local source (WeatherService) and push the image to the registry

                await RunAcrPurgeAsync(options).ConfigureAwait(false);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Failed with the following error:");
                Console.WriteLine(ex);
                return -1;
            }
        }

        private static async Task RunAcrPurgeAsync(Options options)
        {
            var registryOptions = options.Registry;
            var deleteOption = options.Delete;

            Console.WriteLine($"Run ACR Purge");
            Console.WriteLine($"  MIClientId: {registryOptions.MIClientId}");
            Console.WriteLine($"  SPClientId: {registryOptions.SPClientId}");
            Console.WriteLine($"  AzureEnvironment: {registryOptions.AzureEnvironment.Name}");
            Console.WriteLine($"  SubscriptionId: {registryOptions.SubscriptionId}");
            Console.WriteLine($"  ResourceGroupName: {registryOptions.ResourceGroupName}");
            Console.WriteLine($"  RegistyName: {registryOptions.RegistryName}");
            Console.WriteLine($"======================================================================");
            Console.WriteLine();

            var azUtility = new AzureUtility(
                registryOptions.AzureEnvironment,
                registryOptions.TenantId,
                registryOptions.SubscriptionId,
                registryOptions.MIClientId,
                registryOptions.SPClientId,
                registryOptions.SPClientSecret);

            var registryManagementClient = azUtility.RegistryManagementClient;
            var registryClient = AzureUtility.NewAcrClient(deleteOption.AcrName, registryOptions.SPClientId, registryOptions.SPClientSecret); // Managed Identity?

            var toDeleteManifests = (await GetUnreferencedManifestsAsync(registryClient, deleteOption.RepositoryName, deleteOption.Tags)).ToArray();
            

            Console.WriteLine($"{DateTimeOffset.Now}: Starting to untag");
            foreach(var tag in deleteOption.Tags)
            {
                await RunAndWaitCommand(deleteOption.BuildUntagCommand(tag), registryManagementClient, registryOptions, deleteOption);
            }

            Console.WriteLine($"{DateTimeOffset.Now}: Starting to delete");
            foreach(var digest in toDeleteManifests)
            {
                await RunAndWaitCommand(deleteOption.BuildDeleteManifestCommand(digest), registryManagementClient, registryOptions, deleteOption);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        #region Private
        private static async Task RunAndWaitCommand(string cmd, ContainerRegistryManagementClient registryManagementClient, RegistryOptions registryOptions, DeleteOptions deleteOption) {
            string taskString =
$@"
version: v1.1.0
steps:
  - cmd: {cmd}";

            Console.WriteLine($"  Running command: {cmd}");
            var run = await registryManagementClient.Registries.ScheduleRunAsync(
                registryOptions.ResourceGroupName,
                registryOptions.RegistryName,
                new EncodedTaskRunRequest
                {
                    EncodedTaskContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(taskString)),
                    Platform = new PlatformProperties(OS.Linux),
                    Timeout = deleteOption.TimeoutInSeconds,
                    AgentConfiguration = new AgentProperties(cpu: 2)
                }).ConfigureAwait(false);

            Console.WriteLine($"{DateTimeOffset.Now}: Started run: '{run.RunId}'");

            // Poll the run status and wait for completion
            DateTimeOffset deadline = DateTimeOffset.Now.AddMinutes(10);
            while (RunInProgress(run.Status)
                && deadline >= DateTimeOffset.Now)
            {
                Console.WriteLine($"{DateTimeOffset.Now}: In progress: '{run.Status}'. Wait 10 seconds");
                await Task.Delay(10000).ConfigureAwait(false);
                run = await registryManagementClient.Runs.GetAsync(
                    registryOptions.ResourceGroupName,
                    registryOptions.RegistryName,
                    run.RunId).ConfigureAwait(false);
            }

            Console.WriteLine($"{DateTimeOffset.Now}: Run status: '{run.Status}'");

            // Download the run log
            var logResult = await registryManagementClient.Runs.GetLogSasUrlAsync(
                registryOptions.ResourceGroupName,
                registryOptions.RegistryName,
                run.RunId).ConfigureAwait(false);

            using (var httpClient = new HttpClient())
            {
                // NOTE: You can also use azure storage sdk to download the log
                Console.WriteLine($"{DateTimeOffset.Now}: Run log: ");
                var log = await httpClient.GetStringAsync(logResult.LogLink).ConfigureAwait(false);
                Console.WriteLine(log);
            }
            Console.ReadLine();

        }

        private static Options LoadOptions(string appSettingsFile)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(appSettingsFile, optional: true)
                .AddEnvironmentVariables();

            var options = new Options();

            builder.Build().Bind(options);

            return options;
        }

        private static bool RunInProgress(string runStatus)
        {
            return runStatus == RunStatus.Queued
                || runStatus == RunStatus.Started
                || runStatus == RunStatus.Running;
        }

        private static void ChangeValue<T>(Dictionary<T, int> d, T key, int delta) {
            var cnt = d.GetValueOrDefault(key, 0);
            d[key] = cnt + delta;
        }

        private static async Task<IEnumerable<string>> GetUnreferencedManifestsAsync(AzureContainerRegistryClient client, string repositoryName, IEnumerable<string> toDeleteTags)
        {
            var tagList = await client.Tag.GetListAsync(repositoryName);
            var tagToDigest = new Dictionary<string, string>();
            var digests = tagList.Tags.Select(t =>
            {
                tagToDigest.Add(t.Name, t.Digest);
                return t.Digest;
            }).ToArray(); 

            var invalidTags = toDeleteTags.Where(t => tagToDigest.GetValueOrDefault(t, null) == null);
            if(invalidTags.Any())
            {
                throw new Exception($"Invalid tag name: {string.Join(",", invalidTags)}");
            }

            var manifests = await Task.WhenAll( digests.Select(d => client.Manifests.GetAsync(repositoryName, d)));
            var digestToRefCnt = new Dictionary<string, int>();
            var digestToManifest = new Dictionary<string, ManifestWrapper>();

            for(var i = 0; i < manifests.Length; i++)
            {
                var currentManifest = manifests[i];
                var currentDigest = digests[i];

                // Add direct reference
                ChangeValue(digestToRefCnt, currentDigest, 1);
                digestToManifest.TryAdd(currentDigest, currentManifest);
                if (currentManifest.Manifests == null) continue;

                foreach (var m in currentManifest.Manifests)
                {
                    // Add indirect reference in manifest list
                    ChangeValue(digestToRefCnt, m.Digest, 1);
                }
            }

            foreach(var tag in toDeleteTags.Distinct())
            {
                var digest = tagToDigest[tag];
                // Remove direct reference 
                ChangeValue(digestToRefCnt, tagToDigest[tag], -1);
                if (digestToManifest[digest]?.Manifests == null) continue;

                foreach (var m in digestToManifest[digest]?.Manifests)
                {
                    // Remove indirect reference in manifest list
                    ChangeValue(digestToRefCnt, m.Digest, -1);
                }
            }

            return digestToRefCnt.Keys.Where(k => digestToRefCnt[k] == 0);
        }
        #endregion
    }
}
