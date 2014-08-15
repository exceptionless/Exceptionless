using System;
using Exceptionless.Models.Data;

namespace Exceptionless.Services {
    internal class DefaultEnvironmentInfoCollector : IEnvironmentInfoCollector {
        private static EnvironmentInfo _environmentInfo;

        public EnvironmentInfo GetEnvironmentInfo() {
            if (_environmentInfo == null)
                _environmentInfo = new EnvironmentInfo {
                    MachineName = Guid.NewGuid().ToString("N")
                };

            return _environmentInfo;
        }
    }
}
