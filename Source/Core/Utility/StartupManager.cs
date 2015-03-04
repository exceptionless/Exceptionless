using System;

namespace Exceptionless.Core.Utility {
    public class NullStartupManager : IStartupManager {
        public void Startup(object value) {}
    }

    public interface IStartupManager {
        void Startup(object value = null);
    }
}
