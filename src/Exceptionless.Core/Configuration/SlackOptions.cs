using System;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration {
    public class SlackOptions {
        public string SlackId { get; internal set; }

        public string SlackSecret { get; internal set; }

        public bool EnableSlack => !String.IsNullOrEmpty(SlackId);

        public static SlackOptions ReadFromConfiguration(IConfiguration config) {
            var options = new SlackOptions();

            var oAuth = config.GetConnectionString("OAuth").ParseConnectionString();
            options.SlackId = oAuth.GetString(nameof(options.SlackId));
            options.SlackSecret = oAuth.GetString(nameof(options.SlackSecret));

            return options;
        }
    }
}