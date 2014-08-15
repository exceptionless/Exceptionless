#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class StackFrame : Method {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }
    }
}