using System;
using ElasticMacros;

namespace Exceptionless.Core.Repositories.Configuration {
    public interface IHaveMacros {
        void ConfigureMacros(ElasticMacrosConfiguration configuration);
    }
}