using System;

namespace AcrPurge
{
    public class Options
    {
        public RegistryOptions Registry { get; set; }
        public DeleteOptions Delete { get; set; }

        public void Validate()
        {
            if (Registry == null)
            {
                throw new ArgumentNullException(nameof(Registry));
            }

            if (Delete == null)
            {
                throw new ArgumentNullException(nameof(Delete));
            }

            Registry.Validate();

            Delete.Validate();
        }
    }
}
