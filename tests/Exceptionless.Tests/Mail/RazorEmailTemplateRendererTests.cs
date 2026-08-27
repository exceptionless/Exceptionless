using Exceptionless.EmailTemplates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Mail;

public sealed class RazorEmailTemplateRendererTests
{
    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RazorEmailTemplateRenderer(null!, NullLoggerFactory.Instance));
    }

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new RazorEmailTemplateRenderer(serviceProvider, null!));
    }
}
