#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Extras {
    internal static class StringExtensions {
        public static string[] SplitAndTrim(this string input, params char[] separator) {
            if (String.IsNullOrEmpty(input))
                return new string[0];

            var result = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < result.Length; i++)
                result[i] = result[i].Trim();

            return result;
        }
    }
}