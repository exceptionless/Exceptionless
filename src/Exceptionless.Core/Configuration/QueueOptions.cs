using System;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Configuration {
    public class QueueOptions {
        public string ConnectionString { get; internal set; }
        public string QueueScope { get; internal set; }

        public string QueueScopePrefix { get; internal set; }
    }

    public class ConfigureQueueOptions : IConfigureOptions<QueueOptions> {
        private readonly IConfiguration _configuration;
        private readonly AppOptions _appOptions;

        public ConfigureQueueOptions(IConfiguration configuration, AppOptions appOptions) {
            _configuration = configuration;
            _appOptions = appOptions;
        }

        public void Configure(QueueOptions options) {
            options.ConnectionString = _configuration.GetConnectionString("Queue");

            options.QueueScope = _configuration.GetValue(nameof(options.QueueScope), String.Empty);
            options.QueueScopePrefix = !String.IsNullOrEmpty(options.QueueScope) ? options.QueueScope + "-" : _appOptions.AppScopePrefix;
        }
    }
}