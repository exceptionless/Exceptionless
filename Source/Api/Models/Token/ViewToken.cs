using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewToken : NewToken, IIdentity {
        public string Id { get; set; }
        public string Refresh { get; set; }
        public HashSet<string> Scopes { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; } 
    }
}