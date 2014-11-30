using System;
using Exceptionless.Core;
using Exceptionless.Core.Queues;
using StackExchange.Redis;

namespace Exceptionless.Api.Tests.Queue {
    public class RedisQueueTests : InMemoryQueueTests {
        private ConnectionMultiplexer _muxer;

        protected override IQueue<SimpleWorkItem> GetQueue(int retries, TimeSpan? workItemTimeout, TimeSpan? retryDelay) {
            //if (!Settings.Current.UseAzureServiceBus)
            //      return;

            if (_muxer == null)
                _muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionInfo.ToString());

            return new RedisQueue<SimpleWorkItem>(_muxer, workItemTimeout: workItemTimeout, retries: 1);
        }
    }
}