using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class MessageBusOptions {
        public string ConnectionString { get; internal set; }
        public string Provider { get; internal set; }
        public Foundatio.Utility.DataDictionary Data { get; internal set; }
    }

    public class ConfigureMessageBusOptions : IConfigureOptions<MessageBusOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureMessageBusOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(MessageBusOptions options) {
            string cs = _configuration.GetConnectionString("messagebus");
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider).ToLowerInvariant());
            options.ConnectionString = options.Data.GetString(nameof(options.ConnectionString).ToLowerInvariant());
        }
    }
}