using System.Text;
using System.Text.RegularExpressions;
using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Utility.Handlers;

internal sealed partial class SpaIndexHtmlMiddleware
{
    private const string RootIndexFilePath = "index.html";
    private const string SvelteIndexFilePath = "next/index.html";
    private static readonly PathString _rootIndexRequestPath = new("/index.html");
    private static readonly PathString _svelteIndexRequestPath = new("/next/index.html");
    private readonly IFileProvider _fileProvider;
    private readonly RequestDelegate _next;

    public SpaIndexHtmlMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        : this(next, environment.WebRootFileProvider) { }

    internal SpaIndexHtmlMiddleware(RequestDelegate next, IFileProvider fileProvider)
    {
        _next = next;
        _fileProvider = fileProvider;
    }

    public async Task InvokeAsync(HttpContext context, ICspNonceService nonceService)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        string? filePath = GetIndexFilePath(context.Request.Path);
        if (filePath is null)
        {
            await _next(context);
            return;
        }

        IFileInfo file = _fileProvider.GetFileInfo(filePath);
        if (!file.Exists)
        {
            await _next(context);
            return;
        }

        string html;
        await using (Stream stream = file.CreateReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            html = await reader.ReadToEndAsync(context.RequestAborted);

        string responseHtml = AddScriptNonce(html, nonceService.GetNonce());
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(responseHtml);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Remove(HeaderNames.ETag);
        context.Response.Headers.Remove(HeaderNames.LastModified);

        if (!HttpMethods.IsHead(context.Request.Method))
            await context.Response.WriteAsync(responseHtml, context.RequestAborted);
    }

    private static string? GetIndexFilePath(PathString requestPath)
    {
        if (requestPath == _rootIndexRequestPath)
            return RootIndexFilePath;

        if (requestPath == _svelteIndexRequestPath)
            return SvelteIndexFilePath;

        return null;
    }

    internal static string AddScriptNonce(string html, string nonce)
    {
        return ScriptElementRegex().Replace(html, match =>
        {
            string attributes = NonceAttributeRegex().Replace(match.Groups["attributes"].Value, String.Empty);
            return $"<script nonce=\"{nonce}\"{attributes}>{match.Groups["content"].Value}{match.Groups["closingTag"].Value}";
        });
    }

    [GeneratedRegex("<script\\b(?<attributes>(?:\"[^\"]*\"|'[^']*'|[^'\">])*)>(?<content>.*?)(?<closingTag></script\\s*>)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex ScriptElementRegex();

    [GeneratedRegex("\\snonce(?=[\\s=>/]|$)(?:\\s*=\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s>]+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonceAttributeRegex();
}
