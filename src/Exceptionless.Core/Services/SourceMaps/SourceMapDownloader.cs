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
        const string marker = "sourceMappingURL=";
        int markerIndex = generatedContent.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return null;

        int start = markerIndex + marker.Length;
        int end = generatedContent.IndexOfAny(['\r', '\n'], start);
        string value = generatedContent[start..(end < 0 ? generatedContent.Length : end)].Trim();
        if (value.EndsWith("*/", StringComparison.Ordinal))
            value = value[..^2].Trim();
        return value;
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
