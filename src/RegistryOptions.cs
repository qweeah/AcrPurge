using Microsoft.Azure.Management.ResourceManager.Fluent;
using System;

namespace AcrPurge
{
    public class RegistryOptions
    {
        public string Environment { get; set; }
        public string TenantId { get; set; }
        public string MIClientId { get; set; }
        public string SPClientId { get; set; }
        public string SPClientSecret { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroupName { get; set; }
        public string RegistryName { get; set; }

        public AzureEnvironment AzureEnvironment
        {
            get
            {
                return string.IsNullOrWhiteSpace(Environment)
                    ? AzureEnvironment.AzureGlobalCloud
                    : AzureEnvironment.FromName(Environment);
            }
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                throw new ArgumentNullException(nameof(TenantId));
            }

            if (string.IsNullOrWhiteSpace(MIClientId)
                && (string.IsNullOrWhiteSpace(SPClientId) || string.IsNullOrWhiteSpace(SPClientSecret)))
            {
                throw new ArgumentNullException($"Missing {nameof(MIClientId)} or {nameof(SPClientId)}/{nameof(SPClientSecret)}");
            }

            if (string.IsNullOrWhiteSpace(SubscriptionId))
            {
                throw new ArgumentNullException(nameof(SubscriptionId));
            }

            if (string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw new ArgumentNullException(nameof(ResourceGroupName));
            }

            if (string.IsNullOrWhiteSpace(RegistryName))
            {
                throw new ArgumentNullException(nameof(RegistryName));
            }
        }
    }
}
