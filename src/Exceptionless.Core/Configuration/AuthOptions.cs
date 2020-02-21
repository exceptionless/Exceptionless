using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration {
    public class AuthOptions {
        public bool EnableAccountCreation { get; internal set; }
        public bool EnableActiveDirectoryAuth { get; internal set; }

        public string MicrosoftId { get; internal set; }

        public string MicrosoftSecret { get; internal set; }

        public string FacebookId { get; internal set; }

        public string FacebookSecret { get; internal set; }

        public string GitHubId { get; internal set; }

        public string GitHubSecret { get; internal set; }

        public string GoogleId { get; internal set; }

        public string GoogleSecret { get; internal set; }

        public string LdapConnectionString { get; internal set; }

        public static AuthOptions ReadFromConfiguration(IConfiguration config) {
            var options = new AuthOptions();

            options.EnableAccountCreation = config.GetValue(nameof(options.EnableAccountCreation), true);

            options.LdapConnectionString = config.GetConnectionString("LDAP");
            options.EnableActiveDirectoryAuth = config.GetValue(nameof(options.EnableActiveDirectoryAuth), options.LdapConnectionString != null);

            var oAuth = config.GetConnectionString("OAuth").ParseConnectionString();
            options.GoogleId = oAuth.GetString(nameof(options.GoogleId));
            options.GoogleSecret = oAuth.GetString(nameof(options.GoogleSecret));
            options.MicrosoftId = oAuth.GetString(nameof(options.MicrosoftId));
            options.MicrosoftSecret = oAuth.GetString(nameof(options.MicrosoftSecret));
            options.FacebookId = oAuth.GetString(nameof(options.FacebookId));
            options.FacebookSecret = oAuth.GetString(nameof(options.FacebookSecret));
            options.GitHubId = oAuth.GetString(nameof(options.GitHubId));
            options.GitHubSecret = oAuth.GetString(nameof(options.GitHubSecret));

            return options;
        }
    }
}