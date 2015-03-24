using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Extensions {
    public static class ByteArrayExtensions {
        public static byte[] Decompress(this byte[] data, string encoding) {
            byte[] decompressedData = null;
            using (var outputStream = new MemoryStream()) {
                using (var inputStream = new MemoryStream(data)) {
                    if (encoding == "gzip")
                        using (var zip = new GZipStream(inputStream, CompressionMode.Decompress)) {
                            zip.CopyTo(outputStream);
                        }
                    else if (encoding == "deflate")
                        using (var zip = new DeflateStream(inputStream, CompressionMode.Decompress)) {
                            zip.CopyTo(outputStream);
                        }
                    else
                        throw new ArgumentException(String.Format("Unsupported encoding type \"{0}\".", encoding), "encoding");
                }

                decompressedData = outputStream.ToArray();
            }

            return decompressedData;
        }

        public static async Task<byte[]> CompressAsync(this byte[] data, CancellationToken cancellationToken = default(CancellationToken)) {
            byte[] compressesData;
            using (var outputStream = new MemoryStream()) {
                using (var zip = new GZipStream(outputStream, CompressionMode.Compress)) {
                    await zip.WriteAsync(data, 0, data.Length, cancellationToken);
                }

                compressesData = outputStream.ToArray();
            }

            return compressesData;
        }
    }
}