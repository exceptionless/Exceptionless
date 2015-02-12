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
            if (!String.IsNullOrWhiteSpace(identity))
                Identity = identity.Trim();
        }
        
        public UserInfo(string identity, string name) : this(identity) {
            if (!String.IsNullOrWhiteSpace(name))
                Name = name.Trim();
        }

        /// <summary>
        /// Uniquely identifies the user.
        /// </summary>
        public string Identity { get; set; }

        /// <summary>
        /// The Friendly name of the user.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Extended data entries for this user.
        /// </summary>
        public DataDictionary Data { get; set; }
    }
}