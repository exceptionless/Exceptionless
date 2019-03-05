using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core.Configuration {
    public class EmailOptions {
        public bool EnableDailySummary { get; internal set; }

        /// <summary>
        /// All emails that do not match the AllowedOutboundAddresses will be sent to this address in QA mode
        /// </summary>
        public string TestEmailAddress { get; internal set; }

        /// <summary>
        /// Email addresses that match this comma delimited list of domains and email addresses will be allowed to be sent out in QA mode
        /// </summary>
        public List<string> AllowedOutboundAddresses { get; internal set; }

        public string SmtpFrom { get; internal set; }

        public string SmtpHost { get; internal set; }

        public int SmtpPort { get; internal set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SmtpEncryption SmtpEncryption { get; internal set; }

        public string SmtpUser { get; internal set; }

        public string SmtpPassword { get; internal set; }
    }

    public enum SmtpEncryption {
        None,
        StartTLS,
        SSL
    }

    public class ConfigureEmailOptions : IConfigureOptions<EmailOptions> {
        private readonly IConfiguration _configuration;
        private readonly IOptions<AppOptions> _appOptions;

        public ConfigureEmailOptions(IConfiguration configuration, IOptions<AppOptions> appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(EmailOptions options) {
            options.EnableDailySummary = _configuration.GetValue(nameof(options.EnableDailySummary), _appOptions.Value.AppMode == AppMode.Production);
            options.AllowedOutboundAddresses = _configuration.GetValueList(nameof(options.AllowedOutboundAddresses)).Select(v => v.ToLowerInvariant()).ToList();
            options.TestEmailAddress = _configuration.GetValue(nameof(options.TestEmailAddress), "noreply@exceptionless.io");

            var uri = new SmtpUri(_configuration.GetConnectionString("Email") ?? "smtp://localhost");
            options.SmtpHost = uri.Host;
            options.SmtpPort = uri.Port;
            options.SmtpUser = uri.User;
            options.SmtpPassword = uri.Password;
            
            options.SmtpFrom = _configuration.GetValue(nameof(options.SmtpFrom), "Exceptionless <noreply@exceptionless.io>");
            options.SmtpEncryption = _configuration.GetValue(nameof(options.SmtpEncryption), GetDefaultSmtpEncryption(options.SmtpPort));

            if (String.IsNullOrWhiteSpace(options.SmtpUser) != String.IsNullOrWhiteSpace(options.SmtpPassword))
                throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");
        }

        private SmtpEncryption GetDefaultSmtpEncryption(int port) {
            switch (port) {
                case 465:
                    return SmtpEncryption.SSL;
                case 587:
                case 2525:
                    return SmtpEncryption.StartTLS;
                default:
                    return SmtpEncryption.None;
            }
        }
    }
}