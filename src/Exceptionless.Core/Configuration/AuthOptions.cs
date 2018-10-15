using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class AuthOptions {
        public bool EnableAccountCreation { get; internal set; }
        public bool EnableActiveDirectoryAuth { get; internal set; }

        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; internal set; }

        public string MicrosoftAppId { get; internal set; }

        public string MicrosoftAppSecret { get; internal set; }

        public string FacebookAppId { get; internal set; }

        public string FacebookAppSecret { get; internal set; }

        public string GitHubAppId { get; internal set; }

        public string GitHubAppSecret { get; internal set; }

        public string GoogleAppId { get; internal set; }

        public string GoogleAppSecret { get; internal set; }

        public string LdapConnectionString { get; internal set; }
    }

    public class ConfigureAuthOptions : IConfigureOptions<AuthOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureAuthOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(AuthOptions options) {
            options.EnableAccountCreation = _configuration.GetValue(nameof(options.EnableAccountCreation), true);

            options.LdapConnectionString = _configuration.GetConnectionString("Ldap");
            options.EnableActiveDirectoryAuth = _configuration.GetValue(nameof(options.EnableActiveDirectoryAuth), options.LdapConnectionString != null);

            options.IntercomAppSecret = _configuration.GetValue<string>(nameof(options.IntercomAppSecret));
            options.GoogleAppId = _configuration.GetValue<string>(nameof(options.GoogleAppId));
            options.GoogleAppSecret = _configuration.GetValue<string>(nameof(options.GoogleAppSecret));
            options.MicrosoftAppId = _configuration.GetValue<string>(nameof(options.MicrosoftAppId));
            options.MicrosoftAppSecret = _configuration.GetValue<string>(nameof(options.MicrosoftAppSecret));
            options.FacebookAppId = _configuration.GetValue<string>(nameof(options.FacebookAppId));
            options.FacebookAppSecret = _configuration.GetValue<string>(nameof(options.FacebookAppSecret));
            options.GitHubAppId = _configuration.GetValue<string>(nameof(options.GitHubAppId));
            options.GitHubAppSecret = _configuration.GetValue<string>(nameof(options.GitHubAppSecret));
        }
    }
}