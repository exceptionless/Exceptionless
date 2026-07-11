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
    private const int SavedViewPageLimit = 1000;
    private static readonly string[] ValidViewTypes = ["events", "stacks", "stream"];

    private readonly ISavedViewRepository _savedViewRepository;
    private readonly ILockProvider _lockProvider;

    public ForcePredefinedSavedViewsWorkItemHandler(
        ISavedViewRepository savedViewRepository,
        ILockProvider lockProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
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
        var savedViews = await _savedViewRepository.GetPredefinedForForceUpdateAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            predefinedSavedViewsByKey.Keys.ToArray(),
            o => o.SearchAfterPaging().PageLimit(SavedViewPageLimit));

        do
        {
            foreach (var savedViewsByOrganization in savedViews.Documents.GroupBy(savedView => savedView.OrganizationId))
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                var updateResult = await UpdateOrganizationSavedViewsAsync(
                    savedViewsByOrganization.Key,
                    savedViewsByOrganization.Select(savedView => savedView.Id),
                    workItem.UserId,
                    predefinedSavedViewsByKey);

                int updatedForOrganization = updateResult;
                if (updatedForOrganization > 0)
                {
                    organizationsUpdated++;
                    savedViewsUpdated += updatedForOrganization;
                }

                await context.RenewLockAsync();
            }
        } while (!context.CancellationToken.IsCancellationRequested && await savedViews.NextPageAsync());

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

    private async Task<int> UpdateOrganizationSavedViewsAsync(
        string organizationId,
        IEnumerable<string> savedViewIds,
        string userId,
        IReadOnlyDictionary<string, SavedView> predefinedSavedViewsByKey)
    {
        int updatedSavedViews = 0;
        bool lockAcquired = await _lockProvider.TryUsingAsync($"predefined-saved-views:{organizationId}", async () =>
        {
            updatedSavedViews = await UpdateOrganizationSavedViewsWithLockAsync(organizationId, savedViewIds, userId, predefinedSavedViewsByKey);
        }, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));

        if (!lockAcquired)
            Log.LogWarning("Skipping force update for organization {OrganizationId} because the saved view synchronization lock could not be acquired", organizationId);

        return updatedSavedViews;
    }

    private async Task<int> UpdateOrganizationSavedViewsWithLockAsync(
        string organizationId,
        IEnumerable<string> savedViewIds,
        string userId,
        IReadOnlyDictionary<string, SavedView> predefinedSavedViewsByKey)
    {
        var savedViewsToSave = new List<SavedView>();
        var savedViewIdsToUpdate = savedViewIds.ToHashSet(StringComparer.Ordinal);
        var results = await _savedViewRepository.FindAsync(q => q.Organization(organizationId), o => o.PageLimit(SavedViewPageLimit));
        var savedViewsByViewType = results.Documents.GroupBy(savedView => savedView.ViewType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var savedViews in savedViewsByViewType.Values)
        {
            foreach (var savedView in savedViews.Where(savedView => savedViewIdsToUpdate.Contains(savedView.Id)))
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
