using System;
using Exceptionless.Core.Models.Data;

namespace Exceptionless {
    public static class ParameterExtensions {
        public static string GetTypeFullName(this Parameter parameter) {
            if (String.IsNullOrEmpty(parameter.Name))
                return "<UnknownType>";

            return !String.IsNullOrEmpty(parameter.TypeNamespace) ? String.Concat(parameter.TypeNamespace, ".", parameter.Type.Replace('+', '.')) : parameter.Type.Replace('+', '.');
        }
    }
}