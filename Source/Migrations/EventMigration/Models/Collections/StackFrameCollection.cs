using System;
using System.Collections.ObjectModel;

namespace Exceptionless.EventMigration.Models {
    public class StackFrameCollection : Collection<StackFrame> {
        public Core.Models.StackFrameCollection ToStackTrace() {
            var frames = new Core.Models.StackFrameCollection();
            foreach (var item in Items) {
                frames.Add(item.ToStackFrame());
            }

            return frames;
        }
    }
}