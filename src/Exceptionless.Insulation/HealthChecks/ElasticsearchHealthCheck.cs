using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Repositories.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Insulation.HealthChecks {
    public class ElasticsearchHealthCheck : IHealthCheck {
        private readonly ExceptionlessElasticConfiguration _config;
        private readonly ILogger _logger;

        public ElasticsearchHealthCheck(ExceptionlessElasticConfiguration config, ILoggerFactory loggerFactory) {
            _config = config;
            _logger = loggerFactory.CreateLogger<ElasticsearchHealthCheck>();
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            var sw = Stopwatch.StartNew();

            try {
                var pingResult = await _config.Client.LowLevel.PingAsync<PingResponse>(ctx: cancellationToken, requestParameters: new PingRequestParameters {
                    RequestConfiguration = new RequestConfiguration {
                        RequestTimeout = TimeSpan.FromSeconds(60) // 60 seconds is default for NEST
                    }
                });
                bool isSuccess = pingResult.ApiCall.HttpStatusCode == 200;

                return isSuccess ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus);
            } catch (Exception ex) {
                return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
            } finally {
                sw.Stop();
                _logger.LogTrace("Checking Elasticsearch took {Duration:g}", sw.Elapsed);
            }
        }
    }
}