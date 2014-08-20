#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class UserInfo : IData {
        public UserInfo() {
            Data = new DataDictionary();
        }

        public UserInfo(string identity) : this() {
            Identity = identity;
        }

        public string Identity { get; set; }

        /// <summary>
        /// Extended data entries for this user.
        /// </summary>
        public DataDictionary Data { get; set; }
    }
}