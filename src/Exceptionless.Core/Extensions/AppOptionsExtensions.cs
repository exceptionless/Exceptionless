namespace Exceptionless.Core.Extensions;

public static class AppOptionsExtensions
{
    public static string GetApiOrigin(this AppOptions options)
    {
        return new Uri(String.IsNullOrWhiteSpace(options.ApiUrl) ? options.BaseURL : options.ApiUrl).GetLeftPart(UriPartial.Authority);
    }
}
