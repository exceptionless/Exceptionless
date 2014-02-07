#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.IO;
using Exceptionless.Json;

namespace Exceptionless.Serialization {
    internal class JsonTextWriterWithDepth : JsonTextWriter {
        public JsonTextWriterWithDepth(TextWriter textWriter) : base(textWriter) {}

        public int CurrentDepth { get; private set; }

        public override void WriteStartObject() {
            CurrentDepth++;
            base.WriteStartObject();
        }

        public override void WriteEndObject() {
            CurrentDepth--;
            base.WriteEndObject();
        }
    }
}