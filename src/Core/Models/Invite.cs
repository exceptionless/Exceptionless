using System;

namespace Exceptionless.Core.Models {
    public class Invite {
        public string Token { get; set; }
        public string EmailAddress { get; set; }
        public DateTime DateAdded { get; set; }
    }
}