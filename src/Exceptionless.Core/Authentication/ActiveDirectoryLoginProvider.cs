using System.DirectoryServices;
using Exceptionless.Core.Configuration;

namespace Exceptionless.Core.Authentication {
    public class ActiveDirectoryLoginProvider : IDomainLoginProvider {
        private const string AD_EMAIL = "mail";
        private const string AD_FIRSTNAME = "givenName";
        private const string AD_LASTNAME = "sn";
        private const string AD_DISTINGUISHEDNAME = "distinguishedName";
        private const string AD_USERNAME = "sAMAccountName";

        private readonly AuthOptions _authOptions;

        public ActiveDirectoryLoginProvider(AuthOptions authOptions) {
            _authOptions = authOptions;
        } 

        public bool Login(string username, string password) {
            using (var de = new DirectoryEntry(_authOptions.LdapConnectionString, username, password, AuthenticationTypes.Secure)) {
                using (var ds = new DirectorySearcher(de, $"(&({AD_USERNAME}={username}))", new[] { AD_DISTINGUISHEDNAME })) {
                    try {
                        var result = ds.FindOne();
                        return result != null;
                    }
                    // Catch "username and password are invalid"
                    catch (DirectoryServicesCOMException ex) when (ex.ErrorCode == -2147023570) {
                        return false;
                    }
                }
            }
        }

        public string GetUsernameFromEmailAddress(string email) {
            using (var entry = new DirectoryEntry(_authOptions.LdapConnectionString)) {
                using (var searcher = new DirectorySearcher(entry, $"(&({AD_EMAIL}={email}))", new[] { AD_USERNAME })) {
                    var result = searcher.FindOne();
                    return result?.Properties[AD_USERNAME][0].ToString();
                }
            }
        }

        public string GetEmailAddressFromUsername(string username) {
            var result = FindUser(username);
            return result?.Properties[AD_EMAIL][0].ToString();
        }

        public string GetUserFullName(string username) {
            var result = FindUser(username);
            if (result == null)
                return null;

            return $"{result.Properties[AD_FIRSTNAME][0]} {result.Properties[AD_LASTNAME]}";
        }

        private SearchResult FindUser(string username) {
            using (var entry = new DirectoryEntry(_authOptions.LdapConnectionString)) {
                using (var searcher = new DirectorySearcher(entry, $"(&({AD_USERNAME}={username}))", new[] { AD_FIRSTNAME, AD_LASTNAME, AD_EMAIL })) {
                    return searcher.FindOne();
                }
            }
        }
    }
}