using System;
using Exceptionless.Models;

namespace Exceptionless.Api.Models {
    public class ViewToken : NewToken, IIdentity {
        public string Id { get; set; }
        public string Refresh { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; } 
    }
}