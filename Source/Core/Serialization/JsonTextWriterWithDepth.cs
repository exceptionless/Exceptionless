using System;
using System.IO;
#if EMBEDDED
using Exceptionless.Json;
#else
using Newtonsoft.Json;
#endif

namespace Exceptionless.Serializer {
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