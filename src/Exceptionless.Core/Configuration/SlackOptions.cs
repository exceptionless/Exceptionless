using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class SlackOptions {
        public string SlackAppId { get; internal set; }

        public string SlackAppSecret { get; internal set; }

        public bool EnableSlack => !String.IsNullOrEmpty(SlackAppId);
    }

    public class ConfigureSlackOptions : IConfigureOptions<SlackOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureSlackOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(SlackOptions options) {
            options.SlackAppId = _configuration.GetValue<string>(nameof(options.SlackAppId));
            options.SlackAppSecret = _configuration.GetValue<string>(nameof(options.SlackAppSecret));
        }
    }
}