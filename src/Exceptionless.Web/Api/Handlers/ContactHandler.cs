using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Foundatio.Caching;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Handlers;

public class ContactHandler(
    EmailOptions emailOptions,
    IMailer mailer,
    ICacheClient cacheClient,
    TimeProvider timeProvider,
    ILogger<ContactHandler> logger)
{
    private const int ContactRequestLimit = 3;
    private readonly ScopedCacheClient _cache = new(cacheClient, "Contact");

    public async Task<HttpIResult> Handle(SubmitContactRequest message)
    {
        var request = message.Request;
        var context = message.Context;

        if (!String.IsNullOrWhiteSpace(request.Website))
        {
            logger.LogInformation("Contact request ignored because honeypot field was populated from {ClientIpAddress}", context.Request.GetClientIpAddress());
            return HttpResults.Accepted();
        }

        if (String.IsNullOrWhiteSpace(emailOptions.ContactEmailAddress))
            return HttpResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Contact form is not configured.");

        if (await IsRateLimitedAsync(context))
            return ApiResults.TooManyRequests("Too many contact requests. Please try later.");

        bool queued = await mailer.SendContactRequestAsync(
            request.Name!.Trim(),
            request.EmailAddress!.Trim(),
            request.Company?.Trim(),
            request.Subject?.Trim(),
            request.Message!.Trim(),
            context.Request.GetClientIpAddress(),
            context.Request.Headers["User-Agent"].ToString().Truncate(500),
            context.Request.Headers["Referer"].ToString().Truncate(500));

        if (!queued)
            return HttpResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Contact request could not be queued.");

        logger.LogInformation("Contact request queued from {ClientIpAddress}", context.Request.GetClientIpAddress());
        return HttpResults.Accepted();
    }

    private async Task<bool> IsRateLimitedAsync(HttpContext context)
    {
        string clientIpAddress = context.Request.GetClientIpAddress() ?? "unknown";
        string cacheKey = $"ip:{clientIpAddress}:attempts";
        long attempts = await _cache.IncrementAsync(cacheKey, 1, timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        return attempts > ContactRequestLimit;
    }
}
