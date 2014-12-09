using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Api.Models.User {
    public class ViewCurrentUser : ViewUser {
        public ICollection<OAuthAccount> OAuthAccounts { get; set; }
    }
}