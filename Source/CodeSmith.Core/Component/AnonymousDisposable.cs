using System;

namespace CodeSmith.Core.Component
{
    public class AnonymousDisposable : IDisposable
    {
        public Action Action { get; set; }

        void IDisposable.Dispose()
        {
            Action();
        }
    }
}
