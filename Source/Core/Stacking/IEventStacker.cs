using System;
using Exceptionless.Models;

namespace Exceptionless.Core.Stacking {
    public interface IEventStacker {
        void AddSignatureInfo(Event data, SettingsDictionary signatureInfo);
    }
}
