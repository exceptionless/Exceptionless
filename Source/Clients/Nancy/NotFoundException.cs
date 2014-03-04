#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Nancy {
    public class NotFoundException : Exception {
        public NotFoundException() : base("Not found") {}

        internal new int HResult {
            get { return 404; }
        }
    }
}