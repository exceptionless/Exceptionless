using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Stripe;

namespace Exceptionless.Core.Configuration {
    public class StripeOptions {
        public bool EnableBilling => !String.IsNullOrEmpty(StripeApiKey);

        public string StripeApiKey { get; internal set; }

        public string StripeWebHookSigningSecret { get; set; }
    }

    public class ConfigureStripeOptions : IConfigureOptions<StripeOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureStripeOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(StripeOptions options) {
            options.StripeApiKey = _configuration.GetValue<string>(nameof(options.StripeApiKey));
            options.StripeWebHookSigningSecret = _configuration.GetValue<string>(nameof(options.StripeWebHookSigningSecret));
            if (options.EnableBilling)
                StripeConfiguration.SetApiKey(options.StripeApiKey);
        }
    }
}