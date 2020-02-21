using System;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace Exceptionless.Core.Configuration {
    public class StripeOptions {
        public bool EnableBilling => !String.IsNullOrEmpty(StripeApiKey);

        public string StripeApiKey { get; internal set; }

        public string StripeWebHookSigningSecret { get; set; }

        public static StripeOptions ReadFromConfiguration(IConfiguration config) {
            var options = new StripeOptions();

            options.StripeApiKey = config.GetValue<string>(nameof(options.StripeApiKey));
            options.StripeWebHookSigningSecret = config.GetValue<string>(nameof(options.StripeWebHookSigningSecret));
            if (options.EnableBilling)
                StripeConfiguration.ApiKey = options.StripeApiKey;

            return options;
        }
    }
}