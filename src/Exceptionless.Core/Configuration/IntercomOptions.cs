using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class IntercomOptions {
        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomSecret);

        public string IntercomSecret { get; internal set; }
    }

    public class ConfigureIntercomOptions : IConfigureOptions<IntercomOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureIntercomOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(IntercomOptions options) {
            var oAuth = _configuration.GetConnectionString("OAuth").ParseConnectionString();
            options.IntercomSecret = oAuth.GetString(nameof(options.IntercomSecret));
        }
    }
}