using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;

namespace Exceptionless.Api.Utility {
    public class QueryStringParametersParameterBinding : HttpParameterBinding {
        private static readonly HashSet<string> _ignoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_token", "api_key", "apikey" };

        public QueryStringParametersParameterBinding(HttpParameterDescriptor descriptor) : base(descriptor) { }

        public override Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var binding = actionContext.ActionDescriptor.ActionBinding;

            var parameterBinding = binding.ParameterBindings.FirstOrDefault(b => b.Descriptor.ParameterBinderAttribute is QueryStringParametersAttribute);
            if (parameterBinding == null || !(typeof(IDictionary<string, string[]>).IsAssignableFrom(parameterBinding.Descriptor.ParameterType)))
                return Task.FromResult(0);

            var parameters = new Dictionary<string, List<string>>();
            foreach (var pair in actionContext.Request.GetQueryNameValuePairs()) {
                if (_ignoredKeys.Contains(pair.Key))
                    continue;

                string value = pair.Value != null ? Uri.UnescapeDataString(pair.Value) : null;
                if (parameters.ContainsKey(pair.Key))
                    parameters[pair.Key].Add(value);
                else
                    parameters.Add(pair.Key, new List<string> { value });
            }

            SetValue(actionContext, parameters.ToDictionary(k => k.Key, v => v.Value.ToArray()));

            return Task.FromResult(0);
        }

        public override bool WillReadBody => false;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
    public sealed class QueryStringParametersAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            return new QueryStringParametersParameterBinding(parameter);
        }
    }
}
