using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Exceptionless.Insulation.HealthChecks {
    public static class HealthCheckExtensions {
        public static IHealthChecksBuilder Add<T>(this IHealthChecksBuilder builder, string name, params string[] tags) where T : class, IHealthCheck {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return builder.Add(new HealthCheckRegistration(name, s => s.GetRequiredService<T>(), null, tags));
        }
        
        public static IHealthChecksBuilder Add<T>(this IHealthChecksBuilder builder, IServiceCollection services, params string[] tags) where T : class, IHealthCheck {
            return builder.Add<T>(typeof(T).Name, services, tags);
        }
        
        public static IHealthChecksBuilder Add<T>(this IHealthChecksBuilder builder, string name, IServiceCollection services, params string[] tags) where T : class, IHealthCheck {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            services.AddSingleton<T>();
            return builder.Add(new HealthCheckRegistration(name, s => s.GetRequiredService<T>(), null, tags));
        }
    }
}
