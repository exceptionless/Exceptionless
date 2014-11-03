#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions {
    public static class EventExtensions {
        public static T GetValue<T>(this DataDictionary extendedData, string key) {
            if (!extendedData.ContainsKey(key))
                throw new KeyNotFoundException(String.Format("Key \"{0}\" not found in the dictionary.", key));

            object data = extendedData[key];
            if (data is T)
                return (T)data;

            if (data is string) {
                try {
                    return JsonConvert.DeserializeObject<T>((string)data);
                } catch {}
            }

            try {
                return data.ToType<T>();
            } catch {}

            return default(T);
        }
    }
}