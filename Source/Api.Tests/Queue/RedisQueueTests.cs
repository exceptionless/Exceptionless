using System;
using System.Diagnostics;
using Exceptionless.Core;
using Exceptionless.Core.Queues;
using StackExchange.Redis;

namespace Exceptionless.Api.Tests.Queue {
    public class RedisQueueTests : InMemoryQueueTests {
        private ConnectionMultiplexer _muxer;

        protected override IQueue<SimpleWorkItem> GetQueue(int retries = 1, TimeSpan? workItemTimeout = null, TimeSpan? retryDelay = null) {
            //if (!Settings.Current.UseAzureServiceBus)
            //      return;

            if (_muxer == null)
                _muxer = ConnectionMultiplexer.Connect(Settings.Current.RedisConnectionInfo.ToString());

            var queue = new RedisQueue<SimpleWorkItem>(_muxer, workItemTimeout: workItemTimeout, retries: retries, retryDelay: retryDelay);
            Debug.WriteLine(String.Format("Queue Id: {0}", queue.QueueId));
            return queue;
        }
    }
}