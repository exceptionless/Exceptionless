using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class StackRepositoryTests : IntegrationTestsBase
{
    private readonly InMemoryCacheClient _cache;
    private readonly ITextSerializer _serializer;
    private readonly StackData _stackData;
    private readonly IStackRepository _repository;

    public StackRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _cache = GetService<ICacheClient>() as InMemoryCacheClient ?? throw new InvalidOperationException();
        _serializer = GetService<ITextSerializer>();
        _stackData = GetService<StackData>();
        _repository = GetService<IStackRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task CanGetSoftDeletedStack()
    {
        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;

        await _repository.AddAsync(stack, o => o.ImmediateConsistency());

        var actual = await _repository.GetByIdAsync(stack.Id, o => o.Cache("test"));
        Assert.Null(actual);

        actual = await _repository.GetByIdAsync(stack.Id, o => o.Cache("test").IncludeSoftDeletes());
        Assert.NotNull(actual);
    }

    [Fact]
    public async Task CanGetNonExistentStack()
    {
        var stack = await _repository.GetByIdAsync(TestConstants.StackId, o => o.Cache("test"));
        Assert.Null(stack);
    }

    [Fact]
    public async Task CanGetByStatus()
    {
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(organization);

        await _stackData.CreateSearchDataAsync(true);

        var appFilter = new AppFilter(organization);
        var stackIds = await _repository.GetIdsByQueryAsync(q => q.AppFilter(appFilter).FilterExpression("status:open OR status:regressed").DateRange(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow), o => o.PageLimit(o.GetMaxLimit()));
        Assert.Equal(2, stackIds.Total);
    }

    [Fact]
    public async Task CanGetByStackHashAsync()
    {
        long count = _cache.Count;
        long hits = _cache.Hits;
        long misses = _cache.Misses;

        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.UtcNow.SubtractMonths(1)), o => o.Cache());
        Assert.NotNull(stack?.Id);
        Assert.Equal(count + 2, _cache.Count);
        Assert.Equal(hits, _cache.Hits);
        Assert.Equal(misses, _cache.Misses);

        var result = await _repository.GetStackBySignatureHashAsync(stack.ProjectId, stack.SignatureHash);
        JsonAssert.AssertJsonEquivalent(_serializer.SerializeToString(stack), _serializer.SerializeToString(result));
        Assert.Equal(count + 2, _cache.Count);
        Assert.Equal(hits + 1, _cache.Hits);
        Assert.Equal(misses, _cache.Misses);
    }

    [Fact]
    public async Task CanGetByFixedAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId), o => o.ImmediateConsistency());

        var results = await _repository.FindAsync(q => q.FilterExpression("fixed:true"));
        Assert.NotNull(results);
        Assert.Equal(0, results.Total);

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:false"));
        Assert.NotNull(results);
        Assert.Equal(1, results.Total);
        Assert.False(results.Documents.Single().Status == StackStatus.Regressed);
        Assert.Null(results.Documents.Single().DateFixed);

        stack.MarkFixed(null, TimeProvider);
        await _repository.SaveAsync(stack, o => o.ImmediateConsistency());

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:true"));
        Assert.NotNull(results);
        Assert.Equal(1, results.Total);
        Assert.False(results.Documents.Single().Status == StackStatus.Regressed);
        Assert.NotNull(results.Documents.Single().DateFixed);

        results = await _repository.FindAsync(q => q.FilterExpression("fixed:false"));
        Assert.NotNull(results);
        Assert.Equal(0, results.Total);
    }

    [Fact]
    public async Task CanMarkAsRegressedAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, dateFixed: DateTime.UtcNow.SubtractMonths(1)), o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.False(stack.Status == StackStatus.Regressed);
        Assert.NotNull(stack.DateFixed);

        await _repository.MarkAsRegressedAsync(stack.Id);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(stack);
        Assert.True(stack.Status == StackStatus.Regressed);
        Assert.NotNull(stack.DateFixed);
    }

    [Fact]
    public async Task CanIncrementEventCounterAsync()
    {
        var stack = await _repository.AddAsync(_stackData.GenerateStack(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId), o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.Equal(0, stack.TotalOccurrences);
        Assert.True(stack.FirstOccurrence <= DateTime.UtcNow);
        Assert.True(stack.LastOccurrence <= DateTime.UtcNow);
        Assert.NotEqual(DateTime.MinValue, stack.CreatedUtc);
        Assert.NotEqual(DateTime.MinValue, stack.UpdatedUtc);
        Assert.Equal(stack.CreatedUtc, stack.UpdatedUtc);
        var updatedUtc = stack.UpdatedUtc;

        var utcNow = DateTime.UtcNow;
        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow, utcNow, 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(stack);
        Assert.Equal(1, stack.TotalOccurrences);
        Assert.Equal(utcNow, stack.FirstOccurrence);
        Assert.Equal(utcNow, stack.LastOccurrence);
        Assert.Equal(updatedUtc, stack.CreatedUtc);
        Assert.True(updatedUtc.IsBefore(stack.UpdatedUtc), $"Previous {updatedUtc}, Current: {stack.UpdatedUtc}");

        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow.SubtractDays(1), utcNow.SubtractDays(1), 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(stack);
        Assert.Equal(2, stack.TotalOccurrences);
        Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
        Assert.Equal(utcNow, stack.LastOccurrence);

        await _repository.IncrementEventCounterAsync(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, utcNow.AddDays(1), utcNow.AddDays(1), 1);

        stack = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(stack);
        Assert.Equal(3, stack.TotalOccurrences);
        Assert.Equal(utcNow.SubtractDays(1), stack.FirstOccurrence);
        Assert.Equal(utcNow.AddDays(1), stack.LastOccurrence);
    }

    [Fact]
    public async Task SetEventCounterAsync_WhenIncomingValuesAreOlderOrLower_ShouldOnlyApplyMonotonicUpdates()
    {
        // Arrange
        var originalFirst = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var originalLast = new DateTime(2026, 2, 16, 0, 0, 0, DateTimeKind.Utc);
        var stack = await _repository.AddAsync(_stackData.GenerateStack(
            generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            utcFirstOccurrence: originalFirst,
            utcLastOccurrence: originalLast,
            totalOccurrences: 10), o => o.ImmediateConsistency());
        // Act
        await _repository.SetEventCounterAsync(
            stack.Id,
            originalFirst.AddDays(1),
            originalLast.AddDays(-1),
            5,
            sendNotifications: false);

        var unchanged = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(unchanged);

        // Assert
        Assert.Equal(10, unchanged.TotalOccurrences);
        Assert.Equal(originalFirst, unchanged.FirstOccurrence);
        Assert.Equal(originalLast, unchanged.LastOccurrence);

        // Act
        await _repository.SetEventCounterAsync(
            stack.Id,
            originalFirst.AddDays(-1),
            originalLast.AddDays(1),
            15,
            sendNotifications: false);

        var updated = await _repository.GetByIdAsync(stack.Id);
        Assert.NotNull(updated);

        // Assert
        Assert.Equal(15, updated.TotalOccurrences);
        Assert.Equal(originalFirst.AddDays(-1), updated.FirstOccurrence);
        Assert.Equal(originalLast.AddDays(1), updated.LastOccurrence);
    }

    [Fact]
    public async Task CanFindManyAsync()
    {
        await _repository.AddAsync(_stackData.GenerateSampleStacks(), o => o.ImmediateConsistency());

        var stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(1).PageLimit(1));
        Assert.NotNull(stacks);
        Assert.Equal(3, stacks.Total);
        Assert.Single(stacks.Documents);

        var stacks2 = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(2).PageLimit(1));
        Assert.NotNull(stacks);
        Assert.Single(stacks.Documents);

        Assert.NotEqual(stacks.Documents.First().Id, stacks2.Documents.First().Id);

        stacks = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId);
        Assert.NotNull(stacks);
        Assert.Equal(3, stacks.Documents.Count);

        await _repository.RemoveAsync(stacks.Documents, o => o.ImmediateConsistency());
        Assert.Equal(0, await _repository.CountAsync());
    }

    [Fact]
    public async Task GetStacksForCleanupAsync()
    {
        var utcNow = DateTime.UtcNow;
        var openStack10DaysOldWithReference = _stackData.GenerateStack(id: TestConstants.StackId3, utcLastOccurrence: utcNow.SubtractDays(10), status: StackStatus.Open);
        openStack10DaysOldWithReference.References.Add("test");

        await _repository.AddAsync(
            new List<Stack>
            {
                _stackData.GenerateStack(id: TestConstants.StackId, utcLastOccurrence: utcNow.SubtractDays(5),
                    status: StackStatus.Open),
                _stackData.GenerateStack(id: TestConstants.StackId2, utcLastOccurrence: utcNow.SubtractDays(10),
                    status: StackStatus.Open),
                openStack10DaysOldWithReference,
                _stackData.GenerateStack(id: TestConstants.StackId4, utcLastOccurrence: utcNow.SubtractDays(10),
                    status: StackStatus.Fixed)
            }, o => o.ImmediateConsistency());

        var stacks = await _repository.GetStacksForCleanupAsync(TestConstants.OrganizationId, utcNow.SubtractDays(8));
        Assert.NotNull(stacks);
        Assert.Equal(1, stacks.Total);
        Assert.Single(stacks.Documents);
        Assert.Equal(TestConstants.StackId2, stacks.Documents.Single().Id);

        stacks = await _repository.GetStacksForCleanupAsync(TestConstants.OrganizationId, utcNow.SubtractDays(1));
        Assert.NotNull(stacks);
        Assert.Equal(2, stacks.Total);
        Assert.Equal(2, stacks.Documents.Count);
        Assert.NotNull(stacks.Documents.SingleOrDefault(s => String.Equals(s.Id, TestConstants.StackId)));
        Assert.NotNull(stacks.Documents.SingleOrDefault(s => String.Equals(s.Id, TestConstants.StackId2)));
    }

    [Fact]
    public async Task GetDuplicateSignatures_WithDuplicates_ReturnsSignatures()
    {
        string uniqueProjectId = ObjectId.GenerateNewId().ToString();
        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: uniqueProjectId);
        stack1.DuplicateSignature = $"{uniqueProjectId}:dup_sig_test";

        var stack2 = stack1.DeepClone();
        stack2.Id = ObjectId.GenerateNewId().ToString();

        await _repository.AddAsync(new[] { stack1, stack2 }, o => o.ImmediateConsistency());

        var duplicates = await _repository.GetDuplicateSignaturesAsync();
        Assert.Contains($"{uniqueProjectId}:dup_sig_test", duplicates);
    }

    [Fact]
    public async Task GetDuplicateSignatures_WithNoDuplicates_ReturnsEmpty()
    {
        // Use a unique project ID to avoid interference from pre-existing sample data
        string uniqueProjectId = ObjectId.GenerateNewId().ToString();
        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: uniqueProjectId);
        stack1.DuplicateSignature = $"{uniqueProjectId}:unique_sig_1";

        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: uniqueProjectId);
        stack2.DuplicateSignature = $"{uniqueProjectId}:unique_sig_2";

        await _repository.AddAsync(new[] { stack1, stack2 }, o => o.ImmediateConsistency());

        var duplicates = await _repository.GetDuplicateSignaturesAsync();
        // Should not contain our unique signatures since they each appear only once
        Assert.DoesNotContain($"{uniqueProjectId}:unique_sig_1", duplicates);
        Assert.DoesNotContain($"{uniqueProjectId}:unique_sig_2", duplicates);
    }

    [Fact]
    public async Task GetDuplicateSignatures_WithSoftDeletedStacks_ExcludesThem()
    {
        // Use a unique project ID to avoid interference from pre-existing sample data
        string uniqueProjectId = ObjectId.GenerateNewId().ToString();
        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: uniqueProjectId);
        stack1.DuplicateSignature = $"{uniqueProjectId}:softdelete_sig";

        var stack2 = stack1.DeepClone();
        stack2.Id = ObjectId.GenerateNewId().ToString();
        stack2.IsDeleted = true;

        await _repository.AddAsync(new[] { stack1, stack2 }, o => o.ImmediateConsistency());

        var duplicates = await _repository.GetDuplicateSignaturesAsync();
        // The soft-deleted stack should be excluded, leaving only 1 stack with this signature
        Assert.DoesNotContain($"{uniqueProjectId}:softdelete_sig", duplicates);
    }

    [Fact]
    public async Task GetSoftDeleted_WithRedirect_ExcludesRedirectTombstone()
    {
        var source = _stackData.GenerateSampleStack();
        source.IsDeleted = true;
        source.RedirectToStackId = ObjectId.GenerateNewId().ToString();
        source.NeedsRedirectReconciliation = true;
        await _repository.AddAsync(source, o => o.ImmediateConsistency());

        var softDeleted = await _repository.GetSoftDeleted();
        var redirected = await _repository.GetRedirectedStacksNeedingReconciliationAsync();

        Assert.DoesNotContain(softDeleted.Documents, stack => stack.Id == source.Id);
        Assert.Contains(redirected.Documents, stack => stack.Id == source.Id);
    }

    [Fact]
    public async Task GetCanonicalStack_WithRedirect_ReturnsActiveTarget()
    {
        var target = _stackData.GenerateSampleStack();
        var source = target.DeepClone();
        source.Id = ObjectId.GenerateNewId().ToString();
        source.IsDeleted = true;
        source.RedirectToStackId = target.Id;
        await _repository.AddAsync([target, source], o => o.ImmediateConsistency());

        var canonical = await _repository.GetCanonicalStackAsync(source.Id);
        await _repository.AddEventTagsAsync(source.Id, ["redirected-tag"]);
        var updatedTarget = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());

        Assert.NotNull(canonical);
        Assert.Equal(target.Id, canonical.Id);
        Assert.NotNull(updatedTarget);
        Assert.Contains("redirected-tag", updatedTarget.Tags);
    }

    [Fact]
    public async Task SetDuplicateStackRedirect_WithCycle_Throws()
    {
        var stackA = _stackData.GenerateSampleStack();
        var stackB = stackA.DeepClone();
        stackB.Id = ObjectId.GenerateNewId().ToString();
        await _repository.AddAsync([stackA, stackB], o => o.ImmediateConsistency());
        await _repository.SetDuplicateStackRedirectAsync(stackA, stackB.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.SetDuplicateStackRedirectAsync(stackB, stackA.Id));

        var unchangedTarget = await _repository.GetByIdAsync(stackB.Id, o => o.ImmediateConsistency());
        Assert.NotNull(unchangedTarget);
        Assert.Null(unchangedTarget.RedirectToStackId);
    }

    [Fact]
    public async Task Save_WithStaleVersion_CannotOverwriteDuplicateMerge()
    {
        var target = _stackData.GenerateSampleStack();
        target.TotalOccurrences = 100;
        var source = target.DeepClone();
        source.Id = ObjectId.GenerateNewId().ToString();
        source.TotalOccurrences = 10;
        await _repository.AddAsync([target, source], o => o.ImmediateConsistency());

        var staleTarget = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(staleTarget);
        await _repository.MergeDuplicateStackAsync(target.Id, source);

        staleTarget.Title = "stale write";
        await Assert.ThrowsAsync<VersionConflictDocumentException>(() =>
            _repository.SaveAsync(staleTarget, o => o.ImmediateConsistency()));

        var mergedTarget = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(mergedTarget);
        Assert.Equal(110, mergedTarget.TotalOccurrences);
        Assert.Equal(10, mergedTarget.MergedDuplicateStackTotals[source.Id]);
    }

    [Fact]
    public async Task AddEventTags_WithConcurrentCounterUpdates_PreservesBothChanges()
    {
        var stack = _stackData.GenerateSampleStack();
        stack.TotalOccurrences = 0;
        await _repository.AddAsync(stack, o => o.ImmediateConsistency());

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            await Task.WhenAll(
                _repository.AddEventTagsAsync(stack.Id, [$"tag-{index}"]),
                _repository.IncrementEventCounterAsync(
                    stack.OrganizationId,
                    stack.ProjectId,
                    stack.Id,
                    stack.FirstOccurrence,
                    stack.LastOccurrence.AddMinutes(index + 1),
                    1,
                    sendNotifications: false));
        }));

        var updated = await _repository.GetByIdAsync(stack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updated);
        Assert.Equal(20, updated.TotalOccurrences);
        Assert.All(Enumerable.Range(0, 20), index => Assert.Contains($"tag-{index}", updated.Tags));
    }

    [Fact]
    public async Task MarkOpen_WithConcurrentCounterUpdate_PreservesBothChanges()
    {
        var stack = _stackData.GenerateSampleStack();
        stack.Status = StackStatus.Snoozed;
        stack.SnoozeUntilUtc = DateTime.UtcNow.AddMinutes(-1);
        stack.TotalOccurrences = 10;
        await _repository.AddAsync(stack, o => o.ImmediateConsistency());

        await Task.WhenAll(
            _repository.MarkOpenAsync([stack.Id]),
            _repository.IncrementEventCounterAsync(
                stack.OrganizationId,
                stack.ProjectId,
                stack.Id,
                stack.FirstOccurrence,
                stack.LastOccurrence.AddMinutes(1),
                1,
                sendNotifications: false));

        var updated = await _repository.GetByIdAsync(stack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updated);
        Assert.Equal(StackStatus.Open, updated.Status);
        Assert.Null(updated.SnoozeUntilUtc);
        Assert.Equal(11, updated.TotalOccurrences);
    }

    [Fact]
    public async Task MarkAsRegressed_WithConcurrentCounterUpdate_PreservesBothChanges()
    {
        var stack = _stackData.GenerateSampleStack();
        stack.Status = StackStatus.Fixed;
        stack.TotalOccurrences = 10;
        await _repository.AddAsync(stack, o => o.ImmediateConsistency());

        await Task.WhenAll(
            _repository.MarkAsRegressedAsync(stack.Id),
            _repository.IncrementEventCounterAsync(
                stack.OrganizationId,
                stack.ProjectId,
                stack.Id,
                stack.FirstOccurrence,
                stack.LastOccurrence.AddMinutes(1),
                1,
                sendNotifications: false));

        var updated = await _repository.GetByIdAsync(stack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updated);
        Assert.Equal(StackStatus.Regressed, updated.Status);
        Assert.Equal(11, updated.TotalOccurrences);
    }

    [Fact]
    public async Task MergeDuplicateStack_WithRedirectChain_AppliesOnlyLateDelta()
    {
        var stackA = _stackData.GenerateSampleStack();
        stackA.TotalOccurrences = 100;
        var stackB = stackA.DeepClone();
        stackB.Id = ObjectId.GenerateNewId().ToString();
        stackB.TotalOccurrences = 10;
        var stackC = stackA.DeepClone();
        stackC.Id = ObjectId.GenerateNewId().ToString();
        stackC.TotalOccurrences = 50;
        await _repository.AddAsync([stackA, stackB, stackC], o => o.ImmediateConsistency());

        await _repository.MergeDuplicateStackAsync(stackA.Id, stackB);
        await _repository.SetDuplicateStackRedirectAsync(stackB, stackA.Id, isDeleted: true);

        stackA = await _repository.GetByIdAsync(stackA.Id, o => o.ImmediateConsistency()) ?? throw new InvalidOperationException();
        await _repository.MergeDuplicateStackAsync(stackC.Id, stackA);
        await _repository.SetDuplicateStackRedirectAsync(stackA, stackC.Id, isDeleted: true);

        await _repository.IncrementEventCounterAsync(
            stackB.OrganizationId,
            stackB.ProjectId,
            stackB.Id,
            stackB.FirstOccurrence,
            stackB.LastOccurrence.AddMinutes(1),
            5,
            sendNotifications: false);
        stackB = await _repository.GetByIdAsync(stackB.Id, o => o.IncludeSoftDeletes().ImmediateConsistency()) ?? throw new InvalidOperationException();
        await _repository.MergeDuplicateStackAsync(stackC.Id, stackB);

        var canonical = await _repository.GetCanonicalStackAsync(stackB.Id);
        var mergedTarget = await _repository.GetByIdAsync(stackC.Id, o => o.ImmediateConsistency());
        Assert.NotNull(canonical);
        Assert.Equal(stackC.Id, canonical.Id);
        Assert.NotNull(mergedTarget);
        Assert.Equal(165, mergedTarget.TotalOccurrences);
        Assert.Equal(100, mergedTarget.MergedDuplicateStackTotals[stackA.Id]);
        Assert.Equal(15, mergedTarget.MergedDuplicateStackTotals[stackB.Id]);
    }

    [Fact]
    public async Task RemoveAllByProjectId_WithRedirectTombstone_RemovesAllStacks()
    {
        string organizationId = ObjectId.GenerateNewId().ToString();
        string projectId = ObjectId.GenerateNewId().ToString();
        var target = _stackData.GenerateStack(generateId: true, organizationId: organizationId, projectId: projectId);
        var source = target.DeepClone();
        source.Id = ObjectId.GenerateNewId().ToString();
        await _repository.AddAsync([target, source], o => o.ImmediateConsistency());
        await _repository.SetDuplicateStackRedirectAsync(source, target.Id, isDeleted: true);

        await _repository.RemoveAllByProjectIdAsync(organizationId, projectId);

        var remaining = await _repository.GetByIdsAsync([target.Id, source.Id], o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task RemoveAllByOrganizationId_WithRedirectTombstone_RemovesAllStacks()
    {
        string organizationId = ObjectId.GenerateNewId().ToString();
        string projectId = ObjectId.GenerateNewId().ToString();
        var target = _stackData.GenerateStack(generateId: true, organizationId: organizationId, projectId: projectId);
        var source = target.DeepClone();
        source.Id = ObjectId.GenerateNewId().ToString();
        await _repository.AddAsync([target, source], o => o.ImmediateConsistency());
        await _repository.SetDuplicateStackRedirectAsync(source, target.Id, isDeleted: true);

        await _repository.RemoveAllByOrganizationIdAsync(organizationId);

        var remaining = await _repository.GetByIdsAsync([target.Id, source.Id], o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task MergeDuplicateStack_WithRepeatedSource_AppliesMetadataOnce()
    {
        // Arrange
        var target = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId);
        target.CreatedUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        target.LastOccurrence = new DateTime(2026, 1, 2, 1, 0, 0, DateTimeKind.Utc);
        target.TotalOccurrences = 100;
        target.Tags.Add("target");
        target.References.Add("target-reference");

        var source = _stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId);
        source.CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        source.LastOccurrence = new DateTime(2026, 1, 3, 1, 0, 0, DateTimeKind.Utc);
        source.TotalOccurrences = 10;
        source.Status = StackStatus.Fixed;
        source.Tags.Add("source");
        source.References.Add("source-reference");
        source.OccurrencesAreCritical = true;

        await _repository.AddAsync([target, source], o => o.ImmediateConsistency());

        // Act
        DateTime updatedBeforeMerge = target.UpdatedUtc;
        await _repository.MergeDuplicateStackAsync(target.Id, source);

        var targetAfterMerge = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(targetAfterMerge);
        Assert.True(targetAfterMerge.UpdatedUtc > updatedBeforeMerge);

        await _repository.MergeDuplicateStackAsync(target.Id, source);
        var targetAfterNoOp = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(targetAfterNoOp);
        Assert.Equal(targetAfterMerge.UpdatedUtc, targetAfterNoOp.UpdatedUtc);

        // A normal full save must preserve the internal retry ledger.
        targetAfterMerge.Title = "updated after merge";
        await _repository.SaveAsync(targetAfterMerge, o => o.ImmediateConsistency());

        await _repository.MergeDuplicateStackAsync(target.Id, source);

        // Assert
        var merged = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(merged);
        Assert.Equal(110, merged.TotalOccurrences);
        Assert.Equal(source.CreatedUtc, merged.CreatedUtc);
        Assert.Equal(source.LastOccurrence, merged.LastOccurrence);
        Assert.Equal(StackStatus.Fixed, merged.Status);
        Assert.Contains("target", merged.Tags);
        Assert.Contains("source", merged.Tags);
        Assert.Contains("target-reference", merged.References);
        Assert.Contains("source-reference", merged.References);
        Assert.True(merged.OccurrencesAreCritical);

        // Late in-flight occurrences on the redirected source are merged as a delta.
        source.TotalOccurrences = 15;
        source.LastOccurrence = source.LastOccurrence.AddMinutes(1);
        await _repository.SaveAsync(source, o => o.ImmediateConsistency());
        await _repository.MergeDuplicateStackAsync(target.Id, source);

        merged = await _repository.GetByIdAsync(target.Id, o => o.ImmediateConsistency());
        Assert.NotNull(merged);
        Assert.Equal(115, merged.TotalOccurrences);
        Assert.Equal(source.LastOccurrence, merged.LastOccurrence);
    }
}
