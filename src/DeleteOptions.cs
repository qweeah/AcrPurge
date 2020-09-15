using Microsoft.Azure.Management.ResourceManager.Fluent;
using System;
using System.Collections;
using System.Collections.Generic;

namespace AcrPurge
{
    public class DeleteOptions
    {
        private const string DeleteCmd = "acr repository delete";
        private const string UntagCmd = "acr repository untag";
        private const string AcrNameFlag = "--name";
        private const string ImageNameFlag = "--image";

        public string AcrName { get; set; }
        public string RepositoryName { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public bool DryRun { get; set; }
        public int TimeoutInSeconds { get; set; } = 600;

        public void Validate() {
            Console.WriteLine("To-do validation in acr deletion...");
        }
        public string BuildUntagCommand(string tag)
        {
            var cmd = $"{UntagCmd} {AcrNameFlag} {AcrName} {ImageNameFlag} {RepositoryName}:{tag}";

            if (DryRun)
            {
                cmd = cmd + " " + DryRun;
            }

            return cmd;
        }

        public string BuildDeleteManifestCommand(string digest)
        {
            var cmd = $"{DeleteCmd} {AcrNameFlag} {AcrName} {ImageNameFlag} {RepositoryName}@{digest}";

            if (DryRun)
            {
                cmd = cmd + " " + DryRun;
            }

            return cmd;
        }


    }
}
