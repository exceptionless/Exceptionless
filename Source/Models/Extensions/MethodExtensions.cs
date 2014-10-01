#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Text;
using Exceptionless.Models.Data;

namespace Exceptionless {
    public static class MethodExtensions {
        public static string GetDeclaringTypeFullName(this Method method) {
            if (!String.IsNullOrEmpty(method.DeclaringNamespace) && !String.IsNullOrEmpty(method.DeclaringType))
                return String.Concat(method.DeclaringNamespace, ".", method.DeclaringType.Replace('+', '.'));

            if (!String.IsNullOrEmpty(method.DeclaringType))
                return method.DeclaringType.Replace('+', '.');

            return String.Empty;
        }

        public static string GetFullName(this Method method) {
            if (String.IsNullOrEmpty(method.Name))
                return "<null>";

            var sb = new StringBuilder(method.GetDeclaringTypeFullName());
            sb.AppendFormat(".{0}", method.Name);

            if (method.GenericArguments.Count <= 0)
                return sb.ToString();

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

            return sb.ToString();
        }
    }
}