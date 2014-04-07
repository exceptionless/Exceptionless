using System;
using System.Collections.Generic;
using CodeSmith.Core.Component;
using Exceptionless.Models;

namespace Exceptionless.Core.Stacking {
    [Priority(Int32.MaxValue)]
    public class DefaultEventStacker : IEventStacker {
        public void AddSignatureInfo(Event data, IDictionary<string, string> signatureInfo) {
            // only add default signature info if no other signature info has been added
            if (signatureInfo.Count != 0)
                return;

            signatureInfo.Add("Type", data.Type);
            if (!String.IsNullOrEmpty(data.Source))
                signatureInfo.Add("Source", data.Source);
        }
    }
}
