using System;
using System.Collections.Concurrent;
using System.Linq;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;

namespace Exceptionless.Duplicates {
    public class DefaultDuplicateChecker : IDuplicateChecker {
        private readonly ConcurrentQueue<Tuple<int, DateTime>> _recentlyProcessedErrors = new ConcurrentQueue<Tuple<int, DateTime>>();
        private readonly IExceptionlessLog _log;

        public DefaultDuplicateChecker(IExceptionlessLog log) {
            _log = log;
        }

        public bool IsDuplicate(Event ev) {
            if (!ev.IsError())
                return false;

            InnerError current = ev.GetError();
            DateTime repeatWindow = DateTime.Now.AddSeconds(-2);

            while (current != null) {
                int hashCode = current.GetHashCode();
                _log.FormattedTrace(typeof(ExceptionlessClient), "Checking for duplicate error: hash={0} type={1}", hashCode, current.Type);
                _log.FormattedTrace(typeof(ExceptionlessClient), "Error contents: {0}", current.ToString());

                // make sure that we don't process the same error multiple times within 2 seconds.
                if (_recentlyProcessedErrors.Any(s => s.Item1 == hashCode && s.Item2 >= repeatWindow)) {
                    _log.FormattedInfo(typeof(ExceptionlessClient), "Ignoring duplicate exception: type={0}", current.Type);
                    return true;
                }

                // add this exception to our list of recent errors that we have processed.
                _recentlyProcessedErrors.Enqueue(Tuple.Create(hashCode, DateTime.Now));

                // only keep the last 10 recent errors
                Tuple<int, DateTime> temp;
                while (_recentlyProcessedErrors.Count > 10)
                    _recentlyProcessedErrors.TryDequeue(out temp);

                current = current.Inner;
            }

            return false;
        }
    }
}
