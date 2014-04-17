using System;
using System.Threading.Tasks;
using Exceptionless.Dependency;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    public class InMemoryEventQueue : IEventQueue {
        public void Enqueue(Event ev) {
            throw new NotImplementedException();
        }

        public Task ProcessAsync() {

            //_log.Info(typeof(ExceptionlessClient), "Processing queue...");
            //if (!Configuration.Enabled) {
            //    _log.Info(typeof(ExceptionlessClient), "Configuration is disabled. The queue will not be processed.");
            //    //TODO: Should we call StopTimer here?
            //    return;
            //}

            var submissionClient = Configuration.Resolver.GetJsonSerializer();
            throw new NotImplementedException();
        }

        public Configuration Configuration { get; set; }
    }
}
