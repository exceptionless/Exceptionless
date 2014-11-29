using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodeSmith.Core.Threading {
    public class CountDownLatch {
        private int _remaining;
        private EventWaitHandle _event;

        public CountDownLatch(int count) {
            Reset(count);
        }

        public void Reset(int count) {
            if (count < 0)
                throw new ArgumentOutOfRangeException();
            _remaining = count;
            _event = new ManualResetEvent(false);
            if (_remaining == 0)
                _event.Set();
        }

        public void Signal() {
            // The last thread to signal also sets the event.
            if (Interlocked.Decrement(ref _remaining) == 0)
                _event.Set();
        }

        public bool Wait(int millisecondsTimeout) {
            return _event.WaitOne(millisecondsTimeout);
        }

        public int Remaining { get { return _remaining; } }
    }
}
