using System;
using System.ComponentModel;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;
using System.Web.Http.ValueProviders;

namespace Exceptionless.Api.Utility {
    public class CommaDelimitedArrayModelBinder : IModelBinder {
        public bool BindModel(HttpActionContext actionContext, ModelBindingContext bindingContext) {
            ValueProviderResult result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (result == null)
                return false;

            string value = result.RawValue as string;
            if (value == null) {
                bindingContext.Model = Array.CreateInstance(bindingContext.ModelType.GetElementType(), 0);
            } else {
                var elementType = bindingContext.ModelType.GetElementType();
                var converter = TypeDescriptor.GetConverter(elementType);
                var values = value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(converter.ConvertFromString).ToArray();

                var typedValues = Array.CreateInstance(elementType, values.Length);
                values.CopyTo(typedValues, 0);
                bindingContext.Model = typedValues;
            }

            return true;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public sealed class CommaDelimitedArrayAttribute : ParameterBindingAttribute {
        public override HttpParameterBinding GetBinding(HttpParameterDescriptor parameter) {
            if (parameter == null)
                throw new ArgumentException("Invalid parameter");

            return new ModelBinderParameterBinding(parameter, new CommaDelimitedArrayModelBinder(), parameter.Configuration.Services.GetValueProviderFactories());
        }
    }
}
