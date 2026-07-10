using System.Text;
using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class EventPostRequestBodyStreamTests
{
    [Fact]
    public async Task ReadAsync_PayloadAtLimit_CompletesWithoutRejection()
    {
        byte[] payload = Encoding.UTF8.GetBytes("12345");
        await using var stream = new EventPostRequestBodyStream(new MemoryStream(payload), payload.Length);
        await using var destination = new MemoryStream();

        await stream.CopyToAsync(destination, 3, TestContext.Current.CancellationToken);

        Assert.Null(stream.RejectedStatusCode);
        Assert.Equal(payload, destination.ToArray());
    }

    [Fact]
    public async Task ReadAsync_PayloadOverLimit_EndsStreamAndMarksRejected()
    {
        byte[] payload = Encoding.UTF8.GetBytes("123456");
        await using var stream = new EventPostRequestBodyStream(new MemoryStream(payload), 5);
        await using var destination = new MemoryStream();

        await stream.CopyToAsync(destination, 3, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, stream.RejectedStatusCode);
        Assert.True(destination.Length < payload.Length);
    }
}
