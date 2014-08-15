#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Text;

namespace Exceptionless.Models.Data {
    public class Module : IData {
        public Module() {
            Data = new DataDictionary();
        }

        public int ModuleId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsEntry { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DataDictionary Data { get; set; }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(Name);
            sb.Append(", Version=");
            sb.Append(Version);
            if (Data.ContainsKey("PublicKeyToken"))
                sb.Append(", PublicKeyToken=").Append(Data["PublicKeyToken"]);

            return sb.ToString();
        }
    }
}