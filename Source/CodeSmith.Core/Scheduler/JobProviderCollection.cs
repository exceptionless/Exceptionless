using System;
using System.Configuration.Provider;

namespace CodeSmith.Core.Scheduler
{
    public class JobProviderCollection : ProviderCollection
    {
        public override void Add(ProviderBase provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            if (!(provider is JobProvider))
                throw new ArgumentException("Provider must implement JobProvider.");
            base.Add(provider);
        }
    }
}