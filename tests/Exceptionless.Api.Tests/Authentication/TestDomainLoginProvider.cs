using System;
using Exceptionless.Core.Authentication;

namespace Exceptionless.Api.Tests.Authentication {
    internal class TestDomainLoginProvider : IDomainLoginProvider {
        public const string ValidUsername = "user1";
        public const string ValidPassword = "password1!!";

        public bool Login(string username, string password) {
            return username == ValidUsername && password == ValidPassword;
        }

        public string GetEmailAddressFromUsername(string username) {
            return $"{username}@domain.com";
        }

        public string GetUserFullName(string username) {
            return $"{username} {username.ToUpperInvariant()}";
        }

        public string GetUsernameFromEmailAddress(string email) {
            return email == GetEmailAddressFromUsername(ValidUsername) ? ValidUsername : null;
        }
    }
}