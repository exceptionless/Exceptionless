using System;
using Exceptionless.Models.Data;

namespace Exceptionless.Services {
    public interface IEnvironmentInfoCollector {
        EnvironmentInfo GetEnvironmentInfo();
    }
}
