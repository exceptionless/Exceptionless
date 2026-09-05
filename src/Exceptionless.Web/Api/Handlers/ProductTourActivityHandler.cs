using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Mediator;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Serializer;

namespace Exceptionless.Web.Api.Handlers;

public class ProductTourActivityHandler(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IOrganizationRepository organizationRepository,
    UsageService usageService,
    EventPostService eventPostService,
    AppOptions appOptions,
    ISerializer serializer,
    ICacheClient cacheClient,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
{
    private const int ActivityRequestLimit = 100;
    private static readonly TimeSpan ActivityRequestWindow = TimeSpan.FromMinutes(15);
    private readonly ScopedCacheClient _cache = new(cacheClient, "ProductTourActivity");

    private HttpContext HttpContext => httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is unavailable.");

    public async Task<Result> Handle(UpdateCurrentUserProductTourAnalytics message)
    {
        await userRepository.PatchAsync(HttpContext.Request.GetUser().Id,
            new PartialPatch(new { product_tour_analytics_enabled = message.Settings.Enabled!.Value }));
        return Result.NoContent();
    }

    public async Task<Result> Handle(RecordProductTourActivity message)
    {
        var activity = message.Activity;
        if (!ProductTours.IsValid(message.TourName, activity.Version))
        {
            return Result.Invalid(ValidationError.Create("tour_name", "Unknown product tour or unsupported version."));
        }

        if (activity.Step is not null && (!ProductTours.Steps.TryGetValue(message.TourName, out var steps)
            || !steps.Contains(activity.Step, StringComparer.Ordinal)))
        {
            return Result.Invalid(ValidationError.Create("step", "Unknown product tour step."));
        }

        if (activity.Action is ProductTourTelemetryEvent.StepReached && activity.Step is null)
        {
            return Result.Invalid(ValidationError.Create("step", "A reached step is required."));
        }

        // Re-read the preference so a stale browser or authentication cache cannot bypass opt-out.
        var user = await userRepository.GetByIdAsync(HttpContext.Request.GetUser().Id, options => options.Cache(false));
        if (user is null)
        {
            return Result.NotFound("User not found.");
        }

        if (!user.ProductTourAnalyticsEnabled)
        {
            return Result.NoContent();
        }

        // Bound writes into the shared internal project independently of the general API limit.
        var windowStart = timeProvider.GetUtcNow().UtcDateTime.Floor(ActivityRequestWindow);
        long attempts = await _cache.IncrementAsync($"{user.Id}:{windowStart.Ticks}", 1, windowStart.Add(ActivityRequestWindow));
        if (attempts > ActivityRequestLimit)
        {
            return Result.Invalid(ValidationError.Create(ApiValidationErrorIdentifiers.RateLimit, "Too many guided-tour activity requests. Please try later."));
        }

        var project = await projectRepository.GetByIdAsync(appOptions.InternalProjectId, options => options.Cache());
        if (project is null || project.IsDeleted)
        {
            return Result.Unavailable("Guided-tour activity storage is unavailable.");
        }

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, options => options.Cache());
        if (organization is null || organization.IsDeleted || await usageService.GetEventsLeftAsync(organization.Id) < 1)
        {
            return Result.Unavailable("Guided-tour activity storage is unavailable.");
        }

        var ev = new Event
        {
            Type = Event.KnownTypes.FeatureUsage,
            Source = ProductTours.CreateTelemetrySource(activity.Action!.Value, message.TourName, activity.Version, activity.Source!.Value),
            Date = timeProvider.GetUtcNow()
        };
        if (activity.Step is not null)
        {
            ev.Tags!.Add(ProductTours.StepTagPrefix + activity.Step);
        }

        // Do not use the SDK or copy HTTP metadata: these events need no identifying context.
        using var stream = new MemoryStream(serializer.SerializeToBytes(ev));
        // Once accepted, this small server-side write must finish even if navigation closes the request.
        var entry = await eventPostService.EnqueueAsync(new EventPost(appOptions.EnableArchive)
        {
            ApiVersion = 2,
            MediaType = "application/json",
            ProjectId = project.Id,
            OrganizationId = project.OrganizationId
        }, stream);
        return String.IsNullOrEmpty(entry)
            ? Result.Unavailable("Guided-tour activity could not be queued.")
            : Result.Accepted();
    }
}
