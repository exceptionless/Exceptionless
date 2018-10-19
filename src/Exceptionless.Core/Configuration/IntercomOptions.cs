using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class IntercomOptions {
        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; internal set; }
    }

    public class ConfigureIntercomOptions : IConfigureOptions<IntercomOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureIntercomOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(IntercomOptions options) {
            options.IntercomAppSecret = _configuration.GetValue<string>(nameof(options.IntercomAppSecret));
        }
    }
}