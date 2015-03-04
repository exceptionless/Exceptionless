using System;
using System.Linq;
using System.Reflection;
using SimpleInjector.Advanced;

namespace Exceptionless.Core.Utility {
    public class InjectAttributePropertySelectionBehavior : IPropertySelectionBehavior {
        public bool SelectProperty(Type serviceType, PropertyInfo property) {
            return property.GetCustomAttributes(typeof(InjectAttribute), true).Any();
        }
    }
}