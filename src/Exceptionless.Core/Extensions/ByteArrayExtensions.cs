using System.IO.Compression;

namespace Exceptionless.Core.Extensions;

public static class ByteArrayExtensions
{
    public static byte[] Decompress(this byte[] data, string encoding)
    {
        byte[] decompressedData;
        using (var outputStream = new MemoryStream())
        {
            using (var inputStream = new MemoryStream(data))
            {
                if (encoding == "gzip")
                    using (var zip = new GZipStream(inputStream, CompressionMode.Decompress))
                    {
                        zip.CopyTo(outputStream);
                    }
                else if (encoding == "deflate")
                    using (var zip = new DeflateStream(inputStream, CompressionMode.Decompress))
                    {
                        zip.CopyTo(outputStream);
                    }
                else
                    throw new InvalidOperationException($"Unsupported encoding type \"{encoding}\".");
            }

            decompressedData = outputStream.ToArray();
        }

        return decompressedData;
    }

    /// <summary>
    /// Decompresses <paramref name="data"/>, stopping as soon as the output would exceed
    /// <paramref name="maximumBytes"/>.
    /// </summary>
    /// <returns>The decompressed bytes, or <see langword="null"/> when they exceed <paramref name="maximumBytes"/>.</returns>
    public static byte[]? Decompress(this byte[] data, string encoding, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);

        using var inputStream = new MemoryStream(data);
        using Stream decompressionStream = encoding switch
        {
            "gzip" => new GZipStream(inputStream, CompressionMode.Decompress),
            "deflate" => new DeflateStream(inputStream, CompressionMode.Decompress),
            _ => throw new InvalidOperationException($"Unsupported encoding type \"{encoding}\".")
        };

        using var outputStream = new MemoryStream();
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = decompressionStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (outputStream.Length + bytesRead > maximumBytes)
                return null;

            outputStream.Write(buffer, 0, bytesRead);
        }

        return outputStream.ToArray();
    }

    public static byte[] Compress(this byte[] data)
    {
        byte[] compressesData;
        using (var outputStream = new MemoryStream())
        {
            using (var zip = new GZipStream(outputStream, CompressionMode.Compress, true))
            {
                zip.Write(data, 0, data.Length);
            }

            outputStream.Flush();
            compressesData = outputStream.ToArray();
        }

        return compressesData;
    }
}
