#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

#if !EMBEDDED

namespace Exceptionless.Core {
#else
namespace Exceptionless.Net {
#endif

    internal static class ExceptionlessHeaders {
        public const string Basic = "Basic";

        public const string ConfigurationVersion = "v";
        //public const string Authorization = "Authorization";
        //public const string MethodOverride = "X-HTTP-Method-Override";
        //public const string ApiKey = "X-Exceptionless-ApiKey";
        //public const string ResponseStatus = "X-Exceptionless-Status";
        //public const string ResponseContentType = "X-Exceptionless-Content-Type";
    }
}