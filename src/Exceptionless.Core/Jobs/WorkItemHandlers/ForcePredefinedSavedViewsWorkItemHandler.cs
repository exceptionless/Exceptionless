using System.Text.RegularExpressions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class ForcePredefinedSavedViewsWorkItemHandler : WorkItemHandlerBase
{
    private const int OrganizationPageLimit = 100;
    private const int SavedViewPageLimit = 1000;
    private static readonly string[] ValidViewTypes = ["events", "stacks", "stream"];

    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ILockProvider _lockProvider;

    public ForcePredefinedSavedViewsWorkItemHandler(
        IOrganizationRepository organizationRepository,
        ISavedViewRepository savedViewRepository,
        ILockProvider lockProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _savedViewRepository = savedViewRepository;
        _lockProvider = lockProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryAcquireAsync(nameof(ForcePredefinedSavedViewsWorkItemHandler), TimeSpan.FromHours(1), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<ForcePredefinedSavedViewsWorkItem>()!;
        var predefinedSavedViewsByKey = await GetPredefinedSavedViewsByKeyAsync();

        Log.LogInformation(
            "Starting predefined saved view force update. Definitions: {DefinitionCount} InitiatedByUserId: {UserId}",
            predefinedSavedViewsByKey.Count,
            workItem.UserId);

        int organizationsUpdated = 0;
        int savedViewsUpdated = 0;
        var organizations = await _organizationRepository.GetAllAsync(o => o.SearchAfterPaging().PageLimit(OrganizationPageLimit));

        do
        {
            foreach (var organization in organizations.Documents)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                if (String.Equals(organization.Id, PredefinedSavedViewsDataSeed.SystemOrganizationId, StringComparison.Ordinal))
                    continue;

                int updatedForOrganization = await ForceUpdateOrganizationSavedViewsAsync(organization.Id, workItem.UserId, predefinedSavedViewsByKey);
                if (updatedForOrganization > 0)
                {
                    organizationsUpdated++;
                    savedViewsUpdated += updatedForOrganization;
                }

                await context.RenewLockAsync();
            }
        } while (!context.CancellationToken.IsCancellationRequested && await organizations.NextPageAsync());

        Log.LogInformation(
            "Completed predefined saved view force update. OrganizationsUpdated: {OrganizationsUpdated} SavedViewsUpdated: {SavedViewsUpdated} InitiatedByUserId: {UserId}",
            organizationsUpdated,
            savedViewsUpdated,
            workItem.UserId);
    }

    private async Task<Dictionary<string, SavedView>> GetPredefinedSavedViewsByKeyAsync()
    {
        var savedViewsByKey = new Dictionary<string, SavedView>(StringComparer.OrdinalIgnoreCase);

        foreach (string viewType in ValidViewTypes)
        {
            var results = await _savedViewRepository.GetByViewAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId, viewType, o => o.PageLimit(SavedViewPageLimit));
            foreach (var savedView in results.Documents.Where(view => view.UserId is null && !String.IsNullOrWhiteSpace(view.PredefinedKey)))
            {
                if (!savedViewsByKey.TryAdd(savedView.PredefinedKey!, savedView))
                    throw new InvalidOperationException($"Predefined saved view key '{savedView.PredefinedKey}' is duplicated.");
            }
        }

        return savedViewsByKey;
    }

    private async Task<int> ForceUpdateOrganizationSavedViewsAsync(
        string organizationId,
        string userId,
        IReadOnlyDictionary<string, SavedView> predefinedSavedViewsByKey)
    {
        int updatedSavedViews = 0;
        bool lockAcquired = await _lockProvider.TryUsingAsync($"predefined-saved-views:{organizationId}", async () =>
        {
            updatedSavedViews = await ForceUpdateOrganizationSavedViewsWithLockAsync(organizationId, userId, predefinedSavedViewsByKey);
        }, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));

        if (!lockAcquired)
            Log.LogWarning("Skipping force update for organization {OrganizationId} because the saved view synchronization lock could not be acquired", organizationId);

        return updatedSavedViews;
    }

    private async Task<int> ForceUpdateOrganizationSavedViewsWithLockAsync(
        string organizationId,
        string userId,
        IReadOnlyDictionary<string, SavedView> predefinedSavedViewsByKey)
    {
        var savedViewsToSave = new List<SavedView>();

        foreach (string viewType in ValidViewTypes)
        {
            var results = await _savedViewRepository.GetByViewAsync(organizationId, viewType, o => o.PageLimit(SavedViewPageLimit));
            var savedViews = results.Documents.ToList();

            foreach (var savedView in savedViews)
            {
                if (savedView.UserId is not null
                    || String.IsNullOrWhiteSpace(savedView.PredefinedKey)
                    || !predefinedSavedViewsByKey.TryGetValue(savedView.PredefinedKey, out var predefinedSavedView))
                {
                    continue;
                }

                string slug = GetUniqueSlug(predefinedSavedView.Slug, savedViews, savedView.Id);
                if (PredefinedSavedViewConfiguration.Apply(savedView, predefinedSavedView, predefinedSavedView.PredefinedKey!, slug))
                {
                    savedView.UpdatedByUserId = userId;
                    savedViewsToSave.Add(savedView);
                }
            }
        }

        if (savedViewsToSave.Count > 0)
            await _savedViewRepository.SaveAsync(savedViewsToSave, o => o.Cache());

        return savedViewsToSave.Count;
    }

    private static string GetUniqueSlug(string slug, IReadOnlyCollection<SavedView> existingViews, string? excludingId)
    {
        string baseSlug = ToSlug(slug);
        if (String.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "saved-view";

        baseSlug = baseSlug.Length > 100 ? baseSlug[..100].Trim('-') : baseSlug;
        string candidate = baseSlug;
        int suffix = 2;

        while (existingViews.Any(view => view.Id != excludingId && String.Equals(view.Slug, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            string suffixText = $"-{suffix}";
            int maxBaseLength = 100 - suffixText.Length;
            candidate = $"{baseSlug[..Math.Min(baseSlug.Length, maxBaseLength)].Trim('-')}{suffixText}";
            suffix++;
        }

        return candidate;
    }

    private static string ToSlug(string value)
    {
        string slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return Regex.Replace(slug, "-+", "-");
    }
}
