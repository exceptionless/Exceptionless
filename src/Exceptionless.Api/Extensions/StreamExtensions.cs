using System;
using System.IO;

namespace Exceptionless.Api.Extensions {
    public static class StreamExtensions {
        public static byte[] ReadAllBytes(this Stream source) {
            if (source is MemoryStream ms)
                return ms.ToArray();

            using (var memoryStream = new MemoryStream()) {
                source.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}