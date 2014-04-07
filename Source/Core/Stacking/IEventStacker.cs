using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Stacking {
    public interface IEventStacker {
        void AddSignatureInfo(Event data, IDictionary<string, string> signatureInfo);
    }
}
