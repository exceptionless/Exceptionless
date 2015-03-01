#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

#if !EMBEDDED
namespace Exceptionless.Api.Utility {
#else
namespace Exceptionless.Submission.Net {
#endif
    public static class ExceptionlessHeaders {
        public const string Bearer = "Bearer";
        public const string LegacyConfigurationVersion = "v";
        public const string ConfigurationVersion = "X-Exceptionless-ConfigVersion";
        public const string Client = "X-Exceptionless-Client";
        public const string RateLimit = "X-RateLimit-Limit";
        public const string RateLimitRemaining = "X-RateLimit-Remaining";
        public const string LimitedByPlan = "X-LimitedByPlan";
    }
}