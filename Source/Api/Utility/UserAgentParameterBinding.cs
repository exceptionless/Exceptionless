using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Metadata;

namespace Exceptionless.Api.Utility {
    public class UserAgentParameterBinding : HttpParameterBinding {
        public UserAgentParameterBinding(HttpParameterDescriptor descriptor) : base(descriptor) { }

        public override Task ExecuteBindingAsync(ModelMetadataProvider metadataProvider, HttpActionContext actionContext, CancellationToken cancellationToken) {
            var binding = actionContext.ActionDescriptor.ActionBinding;

            var parameterBinding = binding.ParameterBindings.FirstOrDefault(b => b.Descriptor.ParameterBinderAttribute is UserAgentAttribute);
            if (parameterBinding == null || parameterBinding.Descriptor.ParameterType != typeof(string))
                return Task.FromResult(0);

            if (actionContext.Request.Headers.Contains(ExceptionlessHeaders.Client))
                SetValue(actionContext, actionContext.Request.Headers.GetValues(ExceptionlessHeaders.Client).First());
            else
                SetValue(actionContext, actionContext.Request.Headers.UserAgent.ToString());

            return Task.FromResult(0);
        }

        public override bool WillReadBody { get { return false; } }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class UserAgentAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            return new UserAgentParameterBinding(parameter);
        }
    }
}
