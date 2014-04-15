using System;
using System.IO;
using System.IO.Compression;

namespace Exceptionless.Core.Extensions {
    public static class ByteArrayExtensions {
        public static byte[] Decompress(this byte[] data) {
            byte[] decompressedData = null;
            using (var outputStream = new MemoryStream()) {
                using (var inputStream = new MemoryStream(data)) {
                    using (var zip = new GZipStream(inputStream, CompressionMode.Decompress)) {
                        zip.CopyTo(outputStream);
                    }
                }

                decompressedData = outputStream.ToArray();
            }

            return decompressedData;
        }

        public static byte[] Compress(this byte[] data) {
            byte[] compressesData;
            using (var outputStream = new MemoryStream()) {
                using (var zip = new GZipStream(outputStream, CompressionMode.Compress)) {
                    zip.Write(data, 0, data.Length);
                }

                compressesData = outputStream.ToArray();
            }

            return compressesData;
        }
    }
}