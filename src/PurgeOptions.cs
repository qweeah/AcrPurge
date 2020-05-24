using System;

namespace AcrPurge
{
    public class PurgeOptions
    {
        private const string AgoFlag = "--ago";
        private const string DryRunFlag = "--dry-run";
        private const string FilterFlag = "--filter";
        private const string PurgeCmd = "acr purge";
        private const string UntaggedFlag = "--untagged";

        public string Filter { get; set; }
        public string Ago { get; set; }
        public bool Untagged { get; set; }
        public bool DryRun { get; set; }
        public int TimeoutInSeconds { get; set; } = 600;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Filter))
            {
                throw new ArgumentNullException(nameof(Filter));
            }

            if (string.IsNullOrWhiteSpace(Ago))
            {
                throw new ArgumentNullException(nameof(Ago));
            }

            if (TimeoutInSeconds < 1)
            {
                throw new ArgumentException(nameof(TimeoutInSeconds));
            }
        }

        public string BuildCommand()
        {
            var cmd = $"{PurgeCmd} {FilterFlag} {Filter} {AgoFlag} {Ago}";

            if (Untagged)
            {
                cmd = cmd + " " + UntaggedFlag;
            }

            if (DryRun)
            {
                cmd = cmd + " " + DryRun;
            }

            return cmd;
        }
    }
}
