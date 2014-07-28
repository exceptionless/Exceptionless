#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class UserInfo : IData {
        private readonly Lazy<DataDictionary> _data = new Lazy<DataDictionary>(() => new DataDictionary());

        public UserInfo(string identity) {
            Identity = identity;
        }

        public string Identity { get; set; }

        /// <summary>
        /// Extended data entries for this user.
        /// </summary>
        public DataDictionary Data { get { return _data.Value; } }
    }
}