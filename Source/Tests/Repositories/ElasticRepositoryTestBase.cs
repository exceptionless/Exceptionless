using System;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Logging.Xunit;
using Foundatio.Queues;
using Foundatio.Utility;
using Nest;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticRepositoryTestBase : TestWithLoggingBase, IDisposable {
        protected readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly InMemoryCacheClient _cache;
        protected readonly IElasticClient _client;
        protected readonly IQueue<WorkItemData> _workItemQueue;

        public ElasticRepositoryTestBase(ITestOutputHelper output) : base(output) {
            SystemClock.Reset();
            Log.MinimumLevel = LogLevel.Trace;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            _cache = new InMemoryCacheClient(Log);
            _workItemQueue = new InMemoryQueue<WorkItemData>(loggerFactory: Log);
            _configuration = new ExceptionlessElasticConfiguration(_workItemQueue, _cache, Log);
            _client = _configuration.Client;
        }
        
        protected virtual async Task RemoveDataAsync(bool configureIndexes = true) {
            var minimumLevel = Log.MinimumLevel;
            Log.MinimumLevel = LogLevel.Trace;
            
            await _workItemQueue.DeleteQueueAsync();
            await _configuration.DeleteIndexesAsync();
            await _cache.RemoveAllAsync();
            if (configureIndexes)
                await _configuration.ConfigureIndexesAsync();
            
            await _client.RefreshAsync();

            Log.MinimumLevel = minimumLevel;
        }

        public void Dispose() {
            _workItemQueue.Dispose();
            _configuration.Dispose();
            _cache.Dispose();
        }
    }
}