using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route(API_PREFIX + "/contact")]
[AllowAnonymous]
public class ContactController : ExceptionlessApiController
{
    private const int ContactRequestLimit = 3;
    private readonly EmailOptions _emailOptions;
    private readonly IMailer _mailer;
    private readonly ScopedCacheClient _cache;
    private readonly ILogger _logger;

    public ContactController(EmailOptions emailOptions, IMailer mailer, ICacheClient cacheClient, TimeProvider timeProvider, ILogger<ContactController> logger) : base(timeProvider)
    {
        _emailOptions = emailOptions;
        _mailer = mailer;
        _cache = new ScopedCacheClient(cacheClient, "Contact");
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    public Task<IActionResult> PostJsonAsync([FromBody] ContactRequest request)
    {
        return SubmitAsync(request);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public Task<IActionResult> PostFormAsync([FromForm] ContactRequest request)
    {
        return SubmitAsync(request);
    }

    private async Task<IActionResult> SubmitAsync(ContactRequest request)
    {
        if (!String.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("Contact request ignored because honeypot field was populated from {ClientIpAddress}", Request.GetClientIpAddress());
            return Accepted();
        }

        if (String.IsNullOrWhiteSpace(_emailOptions.ContactEmailAddress))
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Contact form is not configured.");

        if (await IsRateLimitedAsync())
            return TooManyRequests("Too many contact requests. Please try later.");

        bool queued = await _mailer.SendContactRequestAsync(
            request.Name!.Trim(),
            request.EmailAddress!.Trim(),
            request.Company?.Trim(),
            request.Subject?.Trim(),
            request.Message!.Trim(),
            Request.GetClientIpAddress(),
            Request.Headers["User-Agent"].ToString().Truncate(500),
            Request.Headers["Referer"].ToString().Truncate(500));

        if (!queued)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Contact request could not be queued.");

        _logger.LogInformation("Contact request queued from {ClientIpAddress}", Request.GetClientIpAddress());
        return Accepted();
    }

    private async Task<bool> IsRateLimitedAsync()
    {
        string clientIpAddress = Request.GetClientIpAddress() ?? "unknown";
        string cacheKey = $"ip:{clientIpAddress}:attempts";
        long attempts = await _cache.IncrementAsync(cacheKey, 1, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(TimeSpan.FromHours(1)));
        return attempts > ContactRequestLimit;
    }
}
