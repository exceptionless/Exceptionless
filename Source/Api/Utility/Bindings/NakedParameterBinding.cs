using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Api.Utility {
    public class NakedBodyParameterBinding : HttpParameterBinding {
        public NakedBodyParameterBinding(HttpParameterDescriptor descriptor) : base(descriptor) {}

        public override async Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var binding = actionContext.ActionDescriptor.ActionBinding;
            
            if (actionContext.Request.Method == HttpMethod.Get || binding.ParameterBindings.Count(b => b.Descriptor.ParameterBinderAttribute is NakedBodyAttribute) > 1)
                return;

            var type = binding.ParameterBindings[0].Descriptor.ParameterType;

            if (type == typeof(string)) {
                string value = await actionContext.Request.Content.ReadAsStringAsync();
                SetValue(actionContext, value);
                return;
            }

            if (type == typeof(byte[])) {
                byte[] value = await actionContext.Request.Content.ReadAsByteArrayAsync();
                SetValue(actionContext, value);
                return;
            }

            throw new InvalidOperationException("Only string and byte[] are supported for [NakedBody] parameters");
        }

        public override bool WillReadBody => true;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class NakedBodyAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            return new NakedBodyParameterBinding(parameter);
        }
    }
}
