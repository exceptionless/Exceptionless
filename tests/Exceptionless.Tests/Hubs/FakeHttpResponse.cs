using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Exceptionless.Tests.Hubs;

/// <summary>
/// A minimal fake HttpResponse for testing SSE connections.
/// Captures written data in a MemoryStream.
/// The WriteAsync extension on HttpResponse writes to Body directly,
/// so the MemoryStream captures all output.
/// </summary>
internal sealed class FakeHttpResponse : HttpResponse, IDisposable
{
    private readonly MemoryStream _body = new();
    private readonly HeaderDictionary _headers = new();

    public override HttpContext HttpContext => null!;
    public override int StatusCode { get; set; }
    public override IHeaderDictionary Headers => _headers;
    public override Stream Body
    {
        get => _body;
        set { }
    }
    public override long? ContentLength { get; set; }
    public override string? ContentType { get; set; }
    public override IResponseCookies Cookies => null!;
    public override bool HasStarted => true;

    /// <summary>
    /// Get all data written to this response as a string.
    /// </summary>
    public string WrittenData => Encoding.UTF8.GetString(_body.ToArray());

    public override void OnCompleted(Func<object, Task> callback, object state) { }
    public override void OnStarting(Func<object, Task> callback, object state) { }
    public override void Redirect(string location, bool permanent) { }

    public void Dispose()
    {
        _body.Dispose();
    }
}
