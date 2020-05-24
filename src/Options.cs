using System;

namespace AcrPurge
{
    public class Options
    {
        public RegistryOptions Registry { get; set; }
        public PurgeOptions Purge { get; set; }

        public void Validate()
        {
            if (Registry == null)
            {
                throw new ArgumentNullException(nameof(Registry));
            }

            if (Purge == null)
            {
                throw new ArgumentNullException(nameof(Purge));
            }

            Registry.Validate();

            Purge.Validate();
        }
    }
}
