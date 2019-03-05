using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Exceptionless.Insulation.HealthChecks {
    public static class HealthCheckExtensions {
        public static IHealthChecksBuilder AddAutoNamedCheck<T>(this IHealthChecksBuilder builder, params string[] tags) where T : class, IHealthCheck {
            var checkType = typeof(T);
            string name = checkType.Name;
            if (checkType.IsConstructedGenericType && checkType.GenericTypeArguments.Length == 1)
                name = checkType.GenericTypeArguments[0].Name;

            if (name.EndsWith("HealthCheck", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 11);
            if (name.EndsWith("Check", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 5);
            if (name.EndsWith("Job", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 3);

            var allTags = new List<string>(tags) { name };
            return builder.AddCheck<T>(name, null, allTags);
        }
    }
}
