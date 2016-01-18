using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;
using Foundatio.Extensions;

namespace Exceptionless.Api.Utility {
    public class QueryStringParametersParameterBinding : HttpParameterBinding {
        public QueryStringParametersParameterBinding(HttpParameterDescriptor descriptor) : base(descriptor) { }

        public override Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var binding = actionContext.ActionDescriptor.ActionBinding;

            var parameterBinding = binding.ParameterBindings.FirstOrDefault(b => b.Descriptor.ParameterBinderAttribute is QueryStringParametersAttribute);
            if (parameterBinding == null || !(typeof(IDictionary<string, string>).IsAssignableFrom(parameterBinding.Descriptor.ParameterType)))
                return Task.FromResult(0);

            var parameters = new Dictionary<string, string>();
            parameters.AddRange(actionContext.Request.GetQueryNameValuePairs().Where(kvp => !kvp.Key.Equals("access_token", StringComparison.OrdinalIgnoreCase)));
            SetValue(actionContext, parameters);

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
