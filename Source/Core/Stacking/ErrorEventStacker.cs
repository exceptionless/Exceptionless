using System;
using CodeSmith.Core.Component;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Stacking {
    [Priority(10)]
    public class ErrorEventStacker : IEventStacker {
        public void AddSignatureInfo(Event ev, SettingsDictionary signatureInfo) {
            if (!ev.Data.ContainsKey("Error"))
                return;

            try {
                var error = JsonConvert.DeserializeObject<Error>(ev.Data["Error"]);
            } catch {}
        }
    }
}