using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Exceptionless.Insulation.HealthChecks {
    public static class HealthCheckExtensions {
        public static IHealthChecksBuilder AddCheck<T>(this IHealthChecksBuilder builder, string name, params string[] tags) where T : class, IHealthCheck {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return builder.AddCheck<T>(name, null, tags);
        }
        
        public static IHealthChecksBuilder AddCheck<T>(this IHealthChecksBuilder builder, params string[] tags) where T : class, IHealthCheck {
            return builder.AddCheck<T>(typeof(T).Name, null, tags);
        }
    }
}
