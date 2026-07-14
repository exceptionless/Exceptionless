namespace Exceptionless.Core.Services.SourceMaps;

internal static class SourceMapContent
{
    public static async Task<byte[]> ReadLimitedAsync(Stream stream, int maximumBytes, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        byte[] buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoryStream.Length + read > maximumBytes)
                throw new InvalidOperationException("The file exceeded the configured maximum size.");

            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return memoryStream.ToArray();
    }
}
