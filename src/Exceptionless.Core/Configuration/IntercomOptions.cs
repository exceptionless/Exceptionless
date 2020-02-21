using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration {
    public class IntercomOptions {
        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomSecret);

        public string IntercomSecret { get; internal set; }

        public static IntercomOptions ReadFromConfiguration(IConfiguration config) {
            var options = new IntercomOptions();

            var oAuth = config.GetConnectionString("OAuth").ParseConnectionString();
            options.IntercomSecret = oAuth.GetString(nameof(options.IntercomSecret));

            return options;
        }
    }
}