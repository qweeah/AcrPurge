using Microsoft.Azure.Management.ContainerRegistry;
using Microsoft.Azure.Management.ContainerRegistry.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Task=System.Threading.Tasks.Task;

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
            var purgeOptions = options.Purge;
            var purgeCmd = purgeOptions.BuildCommand();

            Console.WriteLine($"Run ACR Purge");
            Console.WriteLine($"  MIClientId: {registryOptions.MIClientId}");
            Console.WriteLine($"  SPClientId: {registryOptions.SPClientId}");
            Console.WriteLine($"  AzureEnvironment: {registryOptions.AzureEnvironment.Name}");
            Console.WriteLine($"  SubscriptionId: {registryOptions.SubscriptionId}");
            Console.WriteLine($"  ResourceGroupName: {registryOptions.ResourceGroupName}");
            Console.WriteLine($"  RegistyName: {registryOptions.RegistryName}");
            Console.WriteLine($"  PurgeCommand: {purgeCmd}");
            Console.WriteLine($"  PurgeTaskTimeout: {purgeOptions.TimeoutInSeconds} seconds");
            Console.WriteLine($"======================================================================");
            Console.WriteLine();

            var registryClient = new AzureUtility(
                registryOptions.AzureEnvironment,
                registryOptions.TenantId,
                registryOptions.SubscriptionId,
                registryOptions.MIClientId,
                registryOptions.SPClientId,
                registryOptions.SPClientSecret).RegistryClient;

            Console.WriteLine($"{DateTimeOffset.Now}: Starting new run");

            string taskString =
$@"
version: v1.1.0
steps:
  - cmd: {purgeCmd}
    timeout: {purgeOptions.TimeoutInSeconds}";

            var run = await registryClient.Registries.ScheduleRunAsync(
                registryOptions.ResourceGroupName,
                registryOptions.RegistryName,
                new EncodedTaskRunRequest
                {
                    EncodedTaskContent = Convert.ToBase64String(Encoding.UTF8.GetBytes(taskString)),
                    Platform = new PlatformProperties(OS.Linux),
                    Timeout = 60 * 10, // 10 minutes
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
                run = await registryClient.Runs.GetAsync(
                    registryOptions.ResourceGroupName, 
                    registryOptions.RegistryName, 
                    run.RunId).ConfigureAwait(false);
            }

            Console.WriteLine($"{DateTimeOffset.Now}: Run status: '{run.Status}'");

            // Download the run log
            var logResult = await registryClient.Runs.GetLogSasUrlAsync(
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
        }

        #region Private
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

        #endregion
    }
}
