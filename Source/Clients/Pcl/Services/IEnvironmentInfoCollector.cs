using System;
using Exceptionless.Models;

namespace Exceptionless.Services {
    public interface IEnvironmentInfoCollector {
        EnvironmentInfo GetEnvironmentInfo();
    }
}
