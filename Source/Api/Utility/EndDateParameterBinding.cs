using System;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;
using CodeSmith.Core.Extensions;

namespace Exceptionless.Api.Utility {
    public class EndOfDayModelBinder : IModelBinder {
        public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext) {
            if (bindingContext.ModelType != typeof(DateTime)
                && bindingContext.ModelType != typeof(DateTime?)) {
                return false;
            }

            ValueProviderResult result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (result == null)
                return false;

            string value = result.RawValue as string;
            if (value == null) {
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Wrong value type");
                return false;
            }

            DateTime dateTime;
            if (DateTime.TryParse(value, out dateTime)) {
                if (!value.Contains(":"))
                    dateTime = dateTime.ToEndOfDay();

                bindingContext.Model = dateTime;
                return true;
            }

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Cannot convert value to DateTime");

            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class EndOfDayAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            if (parameter.ParameterType != typeof(DateTime)
                && parameter.ParameterType != typeof(DateTime?))
                throw new ArgumentException("Parameter must be of type DateTime");

            return new ModelBinderParameterBinding(parameter, new EndOfDayModelBinder(), parameter.Configuration.Services.GetValueProviderFactories());
        }
    }
}
