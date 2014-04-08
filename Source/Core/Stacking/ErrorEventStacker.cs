using System;
using System.Collections.Generic;
using CodeSmith.Core.Component;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Core.Stacking {
    [Priority(10)]
    public class ErrorEventStacker : IEventStacker {
        public void AddSignatureInfo(Event ev, IDictionary<string, string> signatureInfo) {
            Error error = ev.GetError();
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