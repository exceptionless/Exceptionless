using System;
using System.Collections;
using System.Collections.Generic;
using CodeSmith.Core.Component;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Stacking {
    [Priority(10)]
    public class ErrorEventStacker : IEventStacker {
        public void AddSignatureInfo(Event ev, IDictionary<string, string> signatureInfo) {
            if (!ev.Data.ContainsKey("Error"))
                return;

            Error error = null;
            try {
                error = JsonConvert.DeserializeObject<Error>(ev.Data.GetString("Error"));
            } catch {}

            if (error == null)
                return;

            var signature = new ErrorSignature(error, userCommonMethods: new[] { "DataContext.SubmitChanges", "Entities.SaveChanges" });
            if (signature.SignatureInfo.Count <= 0)
                return;

            foreach (var key in signature.SignatureInfo.Keys)
                signatureInfo.Add(key, signature.SignatureInfo[key]);
        }
    }
}