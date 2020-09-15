using Microsoft.Azure.ContainerRegistry;
using Microsoft.Azure.Management.ContainerRegistry;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using System;
using System.Net.Http;

namespace AcrPurge
{
    internal class AzureUtility
    {
        private readonly AzureCredentials credential;
        private HttpClient _httpClient = new HttpClient();

        public ContainerRegistryManagementClient RegistryManagementClient { get; private set; }

        public AzureUtility(AzureEnvironment environment, string tenantId, string subscriptionId, string miClientId, string spClientId, string spClientSecret)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentNullException(nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                throw new ArgumentNullException(nameof(subscriptionId));
            }

            if (!string.IsNullOrWhiteSpace(miClientId))
            {
                credential = new AzureCredentials(
                    new MSILoginInformation(MSIResourceType.VirtualMachine, miClientId),
                    environment,
                    tenantId);
            }
            else if (!string.IsNullOrWhiteSpace(spClientId)
                && !string.IsNullOrWhiteSpace(spClientSecret))
            {
                credential = new AzureCredentials(
                    new ServicePrincipalLoginInformation
                    {
                        ClientId = spClientId,
                        ClientSecret = spClientSecret
                    },
                    tenantId,
                    environment);
            }
            else
            {
                throw new ArgumentNullException("No subscription credential");
            }

            RegistryManagementClient = new ContainerRegistryManagementClient(credential.WithDefaultSubscription(subscriptionId));
            RegistryManagementClient.SubscriptionId = subscriptionId;
        }

        public static AzureContainerRegistryClient NewAcrClient(string acrName, string spClientId, string spClientSecret) { 
            var credential = new ContainerRegistryCredentials(ContainerRegistryCredentials.LoginMode.Basic, $"{acrName}.azurecr.io", spClientId, spClientSecret);
            return new AzureContainerRegistryClient(credential, new HttpClient(), disposeHttpClient: false);
        }
    }
}
