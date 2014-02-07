#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Extensions {
    public static class JsonExtensions {
        public static bool Rename(this JObject target, string currentName, string newName) {
            if (target[currentName] == null)
                return false;

            JProperty p = target.Property(currentName);
            target.Remove(p.Name);
            target.Add(newName, p.Value);

            return true;
        }
    }
}