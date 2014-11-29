using System;
using Exceptionless.Core;
using Exceptionless.Core.Queues;
using Microsoft.ServiceBus;

namespace Exceptionless.Api.Tests.Queue {
    public class ServiceBusQueueTests {
        private readonly ServiceBusQueue<SimpleWorkItem> _queue;
        
        public ServiceBusQueueTests() {
          if (!Settings.Current.UseAzureServiceBus)
                return;

            _queue = new ServiceBusQueue<SimpleWorkItem>(Settings.Current.AzureServiceBusConnectionString, 
                "test-_queue", 
                workItemTimeoutMilliseconds: 1000, 
                retries: 1, 
                shouldRecreate: true, 
                retryPolicy: new RetryExponential(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(100), 1));    
        }
    }
}