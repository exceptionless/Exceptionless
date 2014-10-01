#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models.Data;

namespace Exceptionless {
    public static class ParameterExtensions {
        public static string GetTypeFullName(this Parameter parameter) {
            if (String.IsNullOrEmpty(parameter.Name))
                return "<UnknownType>";

            return !String.IsNullOrEmpty(parameter.TypeNamespace) ? String.Concat(parameter.TypeNamespace, ".", parameter.Type.Replace('+', '.')) : parameter.Type.Replace('+', '.');
        }
    }
}