#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class UserDescription : IData {
        public UserDescription(string emailAddress, string description) {
            EmailAddress = emailAddress;
            Description = description;
            Data = new DataDictionary();
        }

        public string EmailAddress { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Extended data entries for this user description.
        /// </summary>
        public DataDictionary Data { get; set; }
    }
}