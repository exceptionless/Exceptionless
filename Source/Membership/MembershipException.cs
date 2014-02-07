#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Runtime.Serialization;

namespace Exceptionless.Membership {
    [Serializable]
    public class MembershipException : Exception {
        public MembershipStatus StatusCode { get; set; }

        public MembershipException() {}

        public MembershipException(string message)
            : base(message) {}

        public MembershipException(string message, Exception inner)
            : base(message, inner) {}

        public MembershipException(MembershipStatus statusCode) {
            StatusCode = statusCode;
        }

        protected MembershipException(SerializationInfo info, StreamingContext context)
            : base(info, context) {}
    }
}