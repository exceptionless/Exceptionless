using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Exceptionless.Core.Configuration;

namespace Exceptionless.Core.Services.SourceMaps;

internal sealed class SourceMapDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SourceMapOptions _options;
    private readonly SourceMapRequestThrottle _throttle;

    public SourceMapDownloader(IHttpClientFactory httpClientFactory, AppOptions options, SourceMapRequestThrottle throttle)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.SourceMapOptions;
        _throttle = throttle;
    }

    public async Task<DownloadedSourceMap?> DownloadAsync(Uri generatedFileUri, bool isRefresh, CancellationToken cancellationToken)
    {
        using var generatedResponse = await DownloadGeneratedFileAsync(generatedFileUri, isRefresh, cancellationToken);
        if (!generatedResponse.Response.IsSuccessStatusCode)
            return null;

        string? sourceMapReference = GetSourceMapHeader(generatedResponse.Response);
        if (String.IsNullOrWhiteSpace(sourceMapReference)
            && (generatedResponse.Response.Content.Headers.ContentLength is not long contentLength
                || contentLength <= _options.MaximumGeneratedFileSize))
        {
            try
            {
                byte[] generatedContent = await SourceMapContent.ReadLimitedAsync(
                    await generatedResponse.Response.Content.ReadAsStreamAsync(cancellationToken),
                    _options.MaximumGeneratedFileSize,
                    cancellationToken);
                sourceMapReference = FindSourceMapReference(Encoding.UTF8.GetString(generatedContent));
            }
            catch (InvalidOperationException)
            {
                // A CDN may ignore the range request and return the entire bundle. Continue to the conventional .map fallback.
            }
        }

        if (String.IsNullOrWhiteSpace(sourceMapReference))
        {
            var fallbackUriBuilder = new UriBuilder(generatedResponse.Uri) { Path = generatedResponse.Uri.AbsolutePath + ".map" };
            var downloaded = await DownloadContentAsync(fallbackUriBuilder.Uri, isRefresh, cancellationToken);
            if (downloaded is not null || String.IsNullOrEmpty(fallbackUriBuilder.Query))
            {
                return downloaded;
            }

            fallbackUriBuilder.Query = String.Empty;
            return await DownloadContentAsync(fallbackUriBuilder.Uri, isRefresh, cancellationToken);
        }

        if (sourceMapReference.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return new DownloadedSourceMap(DecodeDataUri(sourceMapReference, _options.MaximumSourceMapSize), null);

        if (!Uri.TryCreate(generatedResponse.Uri, sourceMapReference, out var sourceMapUri) || !IsAutoDownloadUri(sourceMapUri))
            return null;

        return await DownloadContentAsync(sourceMapUri, isRefresh, cancellationToken);
    }

    private async Task<HttpDownloadResult> DownloadGeneratedFileAsync(Uri generatedFileUri, bool isRefresh, CancellationToken cancellationToken)
    {
        var result = await SendAsync(
            generatedFileUri,
            _options.MaximumGeneratedFileSize,
            validateContentLength: false,
            request => ConfigureGeneratedFileRequest(request, useRange: true),
            isRefresh,
            cancellationToken);
        if (result.Response.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
            return result;

        Uri retryUri = result.Uri;
        result.Dispose();
        return await SendAsync(
            retryUri,
            _options.MaximumGeneratedFileSize,
            validateContentLength: false,
            request => ConfigureGeneratedFileRequest(request, useRange: false),
            isRefresh,
            cancellationToken);
    }

    private static void ConfigureGeneratedFileRequest(HttpRequestMessage request, bool useRange)
    {
        if (useRange)
            request.Headers.Range = new RangeHeaderValue(null, 64 * 1024);
        request.Headers.AcceptEncoding.Clear();
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
    }

    private async Task<DownloadedSourceMap?> DownloadContentAsync(Uri sourceMapUri, bool isRefresh, CancellationToken cancellationToken)
    {
        using var result = await SendAsync(sourceMapUri, _options.MaximumSourceMapSize, validateContentLength: true, null, isRefresh, cancellationToken);
        if (!result.Response.IsSuccessStatusCode)
            return null;

        byte[] content = await SourceMapContent.ReadLimitedAsync(
            await result.Response.Content.ReadAsStreamAsync(cancellationToken),
            _options.MaximumSourceMapSize,
            cancellationToken);
        return new DownloadedSourceMap(content, result.Uri.AbsoluteUri);
    }

    private async Task<HttpDownloadResult> SendAsync(
        Uri uri,
        int maximumBytes,
        bool validateContentLength,
        Action<HttpRequestMessage>? configureRequest,
        bool isRefresh,
        CancellationToken cancellationToken)
    {
        Uri currentUri = uri;
        for (int redirectCount = 0; ; redirectCount++)
        {
            if (!IsAutoDownloadUri(currentUri))
                throw new InvalidOperationException("Source map auto-download only supports public HTTPS URLs.");
            if (!await _throttle.TryReserveOutboundRequestAsync(currentUri, isRefresh))
                throw new SourceMapRequestThrottledException();

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
            configureRequest?.Invoke(request);

            var client = _httpClientFactory.CreateClient(SourceMapService.HttpClientName);
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (validateContentLength && response.Content.Headers.ContentLength > maximumBytes)
            {
                response.Dispose();
                throw new InvalidOperationException("The downloaded file exceeded the configured maximum size.");
            }

            if (!IsRedirect(response.StatusCode))
                return new HttpDownloadResult(currentUri, response);

            if (redirectCount >= _options.MaximumRedirects || response.Headers.Location is null)
            {
                response.Dispose();
                throw new InvalidOperationException("The source map download exceeded the allowed redirects.");
            }

            Uri redirectUri = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(currentUri, response.Headers.Location);
            response.Dispose();
            currentUri = redirectUri;
        }
    }

    private static bool IsAutoDownloadUri(Uri uri)
        => uri.IsDefaultPort && SourceMapService.TryNormalizeGeneratedFileUrl(uri.AbsoluteUri, requireHttps: true, out _);

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is HttpStatusCode.MovedPermanently
        or HttpStatusCode.Redirect
        or HttpStatusCode.RedirectMethod
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    private static string? GetSourceMapHeader(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("SourceMap", out var sourceMapValues))
            return sourceMapValues.FirstOrDefault();
        if (response.Headers.TryGetValues("X-SourceMap", out var legacySourceMapValues))
            return legacySourceMapValues.FirstOrDefault();
        return null;
    }

    private static string? FindSourceMapReference(string generatedContent)
    {
        string? reference = null;
        for (int index = 0; index < generatedContent.Length;)
        {
            char current = generatedContent[index];
            if (current is '\'' or '"' or '`')
            {
                index = SkipQuotedValue(generatedContent, index, current);
                continue;
            }

            if (current != '/' || index + 1 >= generatedContent.Length)
            {
                index++;
                continue;
            }

            char next = generatedContent[index + 1];
            if (next == '/')
            {
                int end = generatedContent.IndexOfAny(['\r', '\n'], index + 2);
                if (end < 0)
                    end = generatedContent.Length;
                reference = ParseSourceMapComment(generatedContent.AsSpan(index + 2, end - index - 2)) ?? reference;
                index = end;
                continue;
            }

            if (next == '*')
            {
                int end = generatedContent.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0)
                    return reference;
                reference = ParseSourceMapComment(generatedContent.AsSpan(index + 2, end - index - 2)) ?? reference;
                index = end + 2;
                continue;
            }

            index++;
        }

        return FindTrailingSourceMapReference(generatedContent) ?? reference;
    }

    private static string? FindTrailingSourceMapReference(string generatedContent)
    {
        ReadOnlySpan<char> content = generatedContent.AsSpan().TrimEnd();
        if (content.IsEmpty)
            return null;

        if (content.EndsWith("*/", StringComparison.Ordinal))
        {
            int commentStart = content.LastIndexOf("/*", StringComparison.Ordinal);
            if (commentStart >= 0)
            {
                string? blockReference = ParseSourceMapComment(content[(commentStart + 2)..^2]);
                if (IsSafeTrailingReference(blockReference))
                    return blockReference;
            }
        }

        int lineStart = content.LastIndexOfAny('\r', '\n') + 1;
        ReadOnlySpan<char> lastLine = content[lineStart..];
        int commentIndex = Math.Max(
            lastLine.LastIndexOf("//#", StringComparison.Ordinal),
            lastLine.LastIndexOf("//@", StringComparison.Ordinal));
        if (commentIndex < 0)
            return null;

        string? lineReference = ParseSourceMapComment(lastLine[(commentIndex + 2)..]);
        return IsSafeTrailingReference(lineReference) ? lineReference : null;
    }

    private static bool IsSafeTrailingReference(string? reference)
        => !String.IsNullOrWhiteSpace(reference) && reference.AsSpan().IndexOfAny(['\'', '"', '`', '\r', '\n']) < 0;

    private static int SkipQuotedValue(string content, int start, char quote)
    {
        for (int index = start + 1; index < content.Length; index++)
        {
            if (content[index] == '\\')
            {
                index++;
                continue;
            }

            if (content[index] == quote)
                return index + 1;
        }

        return content.Length;
    }

    private static string? ParseSourceMapComment(ReadOnlySpan<char> comment)
    {
        comment = comment.Trim();
        if (comment.IsEmpty || comment[0] is not ('#' or '@'))
            return null;

        comment = comment[1..].TrimStart();
        const string marker = "sourceMappingURL";
        if (!comment.StartsWith(marker, StringComparison.Ordinal))
            return null;

        comment = comment[marker.Length..].TrimStart();
        if (comment.IsEmpty || comment[0] != '=')
            return null;

        string value = comment[1..].Trim().ToString();
        return String.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static byte[] DecodeDataUri(string value, int maximumBytes)
    {
        int commaIndex = value.IndexOf(',');
        if (commaIndex < 0)
            throw new FormatException("The inline source map data URI is invalid.");

        string metadata = value[..commaIndex];
        string data = value[(commaIndex + 1)..];
        byte[] decoded = metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(data)
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
        if (decoded.Length > maximumBytes)
            throw new InvalidOperationException("The inline source map exceeded the configured maximum size.");
        return decoded;
    }

    internal sealed record DownloadedSourceMap(byte[] Content, string? SourceMapUrl);

    private sealed record HttpDownloadResult(Uri Uri, HttpResponseMessage Response) : IDisposable
    {
        public void Dispose() => Response.Dispose();
    }
}
