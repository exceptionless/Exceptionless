using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Extensions {
    public static class ByteArrayExtensions {
        public async static Task<byte[]> DecompressAsync(this byte[] data, string encoding) {
            byte[] decompressedData = null;
            using (var outputStream = new MemoryStream()) {
                using (var inputStream = new MemoryStream(data)) {
                    if (encoding == "gzip")
                        using (var zip = new GZipStream(inputStream, CompressionMode.Decompress)) {
                            await zip.CopyToAsync(outputStream).AnyContext();
                        }
                    else if (encoding == "deflate")
                        using (var zip = new DeflateStream(inputStream, CompressionMode.Decompress)) {
                            await zip.CopyToAsync(outputStream).AnyContext();
                        }
                    else
                        throw new ArgumentException($"Unsupported encoding type \"{encoding}\".", nameof(encoding));
                }

                decompressedData = outputStream.ToArray();
            }

            return decompressedData;
        }

        public static async Task<byte[]> CompressAsync(this byte[] data, CancellationToken cancellationToken = default(CancellationToken)) {
            byte[] compressesData;
            using (var outputStream = new MemoryStream()) {
                using (var zip = new GZipStream(outputStream, CompressionMode.Compress, true)) {
                    await zip.WriteAsync(data, 0, data.Length, cancellationToken).AnyContext();
                }

                await outputStream.FlushAsync(cancellationToken).AnyContext();
                compressesData = outputStream.ToArray();
            }

            return compressesData;
        }
    }
}