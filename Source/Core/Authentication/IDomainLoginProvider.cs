using System;

namespace Exceptionless.Core.Authentication {
    public interface IDomainLoginProvider {
        bool Login(string username, string password);

        string GetEmailAddressFromUsername(string username);

        string GetUserFullName(string username);

        string GetUsernameFromEmailAddress(string email);
    }
}