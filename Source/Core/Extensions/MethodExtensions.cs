using System;
using System.Text;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions {
    public static class MethodExtensions {
        public static string GetFullName(this Method method) {
            if (method == null)
                return null;

            var sb = new StringBuilder();
            AppendMethod(method, sb, includeParameters: false);
            return sb.ToString();
        }

        public static string GetSignature(this Method method) {
            if (method == null)
                return null;

            var sb = new StringBuilder();
            AppendMethod(method, sb);
            return sb.ToString();
        }

        internal static void AppendMethod(Method method, StringBuilder sb, bool includeParameters = true) {
            if (String.IsNullOrEmpty(method?.Name)) {
                sb.Append("<null>");
                return;
            }

            if (!String.IsNullOrEmpty(method.DeclaringNamespace))
                sb.Append(method.DeclaringNamespace).Append(".");

            if (!String.IsNullOrEmpty(method.DeclaringType))
                sb.Append(method.DeclaringType.Replace('+', '.')).Append(".");

            sb.Append(method.Name);

            if (method.GenericArguments?.Count > 0) {
                sb.Append("[");
                bool first = true;
                foreach (string arg in method.GenericArguments) {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");

                    sb.Append(arg);
                }

                sb.Append("]");
            }

            if (includeParameters) {
                sb.Append("(");
                bool first = true;
                if (method.Parameters?.Count > 0) {
                    foreach (Parameter p in method.Parameters) {
                        if (first)
                            first = false;
                        else
                            sb.Append(", ");

                        if (String.IsNullOrEmpty(p.Type))
                            sb.Append("<UnknownType>");
                        else
                            sb.Append(p.Type.Replace('+', '.'));

                        sb.Append(" ");
                        sb.Append(p.Name);
                    }
                }
                sb.Append(")");
            }
        }
    }
}
