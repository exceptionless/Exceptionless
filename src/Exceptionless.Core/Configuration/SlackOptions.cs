using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class SlackOptions {
        public string SlackId { get; internal set; }

        public string SlackSecret { get; internal set; }

        public bool EnableSlack => !String.IsNullOrEmpty(SlackId);
    }

    public class ConfigureSlackOptions : IConfigureOptions<SlackOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureSlackOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(SlackOptions options) {
            var oAuth = _configuration.GetConnectionString("OAuth").ParseConnectionString();
            options.SlackId = oAuth.GetString(nameof(options.SlackId));
            options.SlackSecret = oAuth.GetString(nameof(options.SlackSecret));
        }
    }
}