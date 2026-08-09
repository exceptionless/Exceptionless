using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Elastic.Clients.Elasticsearch;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.CustomFields;

public sealed class CustomFieldIndexingTests : IntegrationTestsBase
{
    private readonly EventPipeline _pipeline;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICustomFieldDefinitionRepository _customFieldDefinitionRepository;
    private readonly EventCustomFieldService _eventCustomFieldService;
    private readonly ExceptionlessElasticConfiguration _elasticConfiguration;
    private readonly OrganizationData _organizationData;
    private readonly ProjectData _projectData;
    private readonly UserData _userData;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public CustomFieldIndexingTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _pipeline = GetService<EventPipeline>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _customFieldDefinitionRepository = GetService<ICustomFieldDefinitionRepository>();
        _eventCustomFieldService = GetService<EventCustomFieldService>();
        _elasticConfiguration = GetService<ExceptionlessElasticConfiguration>();
        _organizationData = GetService<OrganizationData>();
        _projectData = GetService<ProjectData>();
        _userData = GetService<UserData>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await CreateProjectDataAsync();
        await _eventCustomFieldService.EnsureSystemFieldsAsync(TestConstants.OrganizationId);
    }

    [Fact]
    public async Task EnsureSystemFields_ReservedSlotOccupied_ThrowsConflict()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), "reserved-slot-organization", "claimed_keyword_slot", "keyword");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _eventCustomFieldService.EnsureSystemFieldsAsync("reserved-slot-organization"));

        Assert.Contains("reserved slot is occupied", exception.Message);
    }

    [Fact]
    public async Task EnsureSystemFields_ReservedNameWithWrongType_ThrowsConflict()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), "wrong-type-organization", "@ref:session", "date");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _eventCustomFieldService.EnsureSystemFieldsAsync("wrong-type-organization"));

        Assert.Contains("index type is 'date'", exception.Message);
    }

    [Fact]
    public async Task EnsureSystemFields_SoftDeletedReservedDefinition_ThrowsConflict()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), "deleted-system-field-organization", "@ref:session", "keyword");
        definition.IsDeleted = true;
        await _customFieldDefinitionRepository.SaveAsync(definition);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _eventCustomFieldService.EnsureSystemFieldsAsync("deleted-system-field-organization"));

        Assert.Contains("definition is soft-deleted", exception.Message);
    }

    [Fact]
    public async Task EnsureSystemFields_DuplicateReservedDefinitions_ThrowsConflict()
    {
        const string organizationId = "duplicate-system-field-organization";
        var original = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), organizationId, "@ref:session", "keyword");
        original.IsDeleted = true;
        await _customFieldDefinitionRepository.SaveAsync(original);

        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), organizationId, "@ref:session", "keyword");
        original.IsDeleted = false;
        await _customFieldDefinitionRepository.SaveAsync(original);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _eventCustomFieldService.EnsureSystemFieldsAsync(organizationId));

        Assert.Contains("found 2 definitions", exception.Message);
    }

    [Fact]
    public async Task EnsureSystemFields_ConcurrentInitialProvisioning_CreatesOneDefinitionPerReservedField()
    {
        const string organizationId = "concurrent-system-field-organization";

        await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => _eventCustomFieldService.EnsureSystemFieldsAsync(organizationId)));

        var results = await _customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), organizationId);
        var definitions = new List<CustomFieldDefinition>();
        do
        {
            definitions.AddRange(results.Documents);
        } while (await results.NextPageAsync());

        Assert.Equal(EventCustomFieldService.SystemFields.Count, definitions.Count);
        foreach (var systemField in EventCustomFieldService.SystemFields)
        {
            var definition = Assert.Single(definitions, field => field.Name == systemField.Name);
            Assert.Equal(systemField.IndexType, definition.IndexType);
            Assert.Equal(systemField.IdxField, definition.GetIdxName());
        }
    }

    [Fact]
    public async Task SessionSystemFieldFilters_ReadCurrentAndLegacyStorage()
    {
        await _eventCustomFieldService.EnsureSystemFieldsAsync(TestConstants.OrganizationId);

        var currentReference = GenerateEvent();
        currentReference.Source = "system-field-reference";
        currentReference.Data = new DataDictionary { ["@ref:session"] = "session-42" };
        var legacyReference = GenerateEvent();
        legacyReference.Source = "system-field-reference";
        legacyReference.Idx = new DataDictionary { ["session-r"] = "session-42" };

        var sessionEnd = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var currentSessionEnd = GenerateEvent();
        currentSessionEnd.Source = "system-field-session-end";
        currentSessionEnd.Data = new DataDictionary { [Event.KnownDataKeys.SessionEnd] = sessionEnd };
        var legacySessionEnd = GenerateEvent();
        legacySessionEnd.Source = "system-field-session-end";
        legacySessionEnd.Idx = new DataDictionary { ["sessionend-d"] = sessionEnd };
        var missingSessionEnd = GenerateEvent();
        missingSessionEnd.Source = "system-field-session-end";

        var currentHasError = GenerateEvent();
        currentHasError.Source = "system-field-has-error";
        currentHasError.Data = new DataDictionary { [Event.KnownDataKeys.SessionHasError] = true };
        var legacyHasError = GenerateEvent();
        legacyHasError.Source = "system-field-has-error";
        legacyHasError.Idx = new DataDictionary { ["haserror-b"] = true };
        var currentNoError = GenerateEvent();
        currentNoError.Source = "system-field-has-error";
        currentNoError.Data = new DataDictionary { [Event.KnownDataKeys.SessionHasError] = false };
        var legacyNoError = GenerateEvent();
        legacyNoError.Source = "system-field-has-error";
        legacyNoError.Idx = new DataDictionary { ["haserror-b"] = false };
        var missingHasError = GenerateEvent();
        missingHasError.Source = "system-field-has-error";

        PersistentEvent[] currentEvents =
        [
                currentReference,
                currentSessionEnd,
                missingSessionEnd,
                currentHasError,
                currentNoError,
                missingHasError
        ];
        PersistentEvent[] legacyEvents = [legacyReference, legacySessionEnd, legacyHasError, legacyNoError];
        foreach (var eventDocument in currentEvents.Concat(legacyEvents))
            eventDocument.StackId = TestConstants.StackId;

        await _eventRepository.AddAsync(currentEvents, o => o.ImmediateConsistency());
        foreach (var legacyEvent in legacyEvents)
            await IndexLegacyEventAsync(legacyEvent);
        await RefreshDataAsync();

        Assert.Equal(2, await CountSystemFieldMatchesAsync("system-field-reference", "ref.session:session-42"));
        Assert.Equal(2, await CountSystemFieldMatchesAsync("system-field-session-end", "_exists_:data.sessionend"));
        Assert.Equal(2, await CountSystemFieldMatchesAsync("system-field-session-end", "data.sessionend:>2025-01-01"));
        Assert.Equal(1, await CountSystemFieldMatchesAsync("system-field-session-end", "_missing_:data.sessionend"));
        Assert.Equal(2, await CountSystemFieldMatchesAsync("system-field-has-error", "data.haserror:true"));
        Assert.Equal(4, await CountSystemFieldMatchesAsync("system-field-has-error", "_exists_:data.haserror"));
        Assert.Equal(1, await CountSystemFieldMatchesAsync("system-field-has-error", "_missing_:data.haserror"));
        Assert.Equal(3, await CountSystemFieldMatchesAsync("system-field-has-error", "NOT data.haserror:true"));
    }

    [Fact]
    public async Task Event_WithMatchingCustomFieldDefinition_GetsIndexed()
    {
        // Arrange: create a custom field definition
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "response_time", "double");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["response_time"] = 123.45;

        // Act: run through pipeline (DocumentsAdding handler populates Idx)
        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        // Assert: verify idx has the custom field on the in-memory event
        Assert.NotNull(context.Event.Idx);
        var idxValues = context.Event.Idx.Values.OfType<double>().ToList();
        Assert.Contains(123.45, idxValues);
    }

    [Fact]
    public async Task Event_DecimalLikeVersionConfiguredAsKeyword_PreservesExactString()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "DatabaseVersion", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data = new DataDictionary { ["DatabaseVersion"] = "4.90" };

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());

        Assert.False(context.HasError, context.ErrorMessage);
        Assert.Equal("4.90", Assert.IsType<string>(context.Event.Data?["DatabaseVersion"]));
        Assert.NotNull(context.Event.Idx);
        Assert.Equal("4.90", Assert.IsType<string>(context.Event.Idx[definition.GetIdxName()]));
    }

    [Fact]
    public async Task SparseStringSlot_AcrossDailyIndexes_RetainsExactAndTermsResolution()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "sparse_label", "string");
        await RefreshDataAsync();

        var priorDayEvent = GenerateEvent(new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(12), TimeSpan.Zero));
        priorDayEvent.Source = "sparse-daily-mapping";
        priorDayEvent.Data = new DataDictionary { ["sparse_label"] = "Acme" };
        var priorDayContext = await _pipeline.RunAsync(priorDayEvent, GetOrganization(), GetProject());
        Assert.False(priorDayContext.HasError, priorDayContext.ErrorMessage);

        // Materialize a newer daily partition without the sparse slot. Exact and terms resolution
        // must still come from the declared pooled mapping, not only the latest server mapping.
        var currentDayEvent = GenerateEvent(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero));
        currentDayEvent.Source = "sparse-daily-mapping";
        var currentDayContext = await _pipeline.RunAsync(currentDayEvent, GetOrganization(), GetProject());
        Assert.False(currentDayContext.HasError, currentDayContext.ErrorMessage);
        await RefreshDataAsync();

        string physicalField = $"idx.{definition.GetIdxName()}";
        Assert.Equal($"{physicalField}.keyword", _elasticConfiguration.Events.MappingResolver.GetNonAnalyzedFieldName(physicalField));

        var exactMatches = await _eventRepository.FindAsync(query => query
            .Organization(TestConstants.OrganizationId)
            .FieldEquals(eventDocument => eventDocument.Source, "sparse-daily-mapping")
            .FilterExpression("data.sparse_label:Acme"));
        Assert.Single(exactMatches.Documents);
        Assert.Equal(priorDayContext.Event.Id, exactMatches.Documents.Single().Id);

        var aggregation = await _eventRepository.CountAsync(query => query
            .Organization(TestConstants.OrganizationId)
            .FieldEquals(eventDocument => eventDocument.Source, "sparse-daily-mapping")
            .AggregationsExpression("terms:data.sparse_label"));
        var terms = aggregation.Aggregations.Terms<string>("terms_data.sparse_label");
        var bucket = Assert.Single(terms?.Buckets ?? []);
        Assert.Equal("Acme", bucket.KeyAsString);
        Assert.Equal(1, bucket.Total);
    }

    [Fact]
    public async Task Event_WithNoMatchingDefinition_DoesNotGetCustomIndexed()
    {
        // No custom field definition created for this org's "unregistered_field"
        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["unregistered_field"] = "value";

        var org = GetOrganization();
        org.HasPremiumFeatures = true;

        var context = await _pipeline.RunAsync(ev, org, GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        // Unregistered fields are not copied to either legacy named fields or managed slots.
        if (context.Event.Idx is not null)
        {
            Assert.DoesNotContain("unregistered_field-s", context.Event.Idx.Keys);
            Assert.DoesNotContain(context.Event.Idx.Keys, k =>
                System.Text.RegularExpressions.Regex.IsMatch(k, @"^(bool|date|double|float|int|keyword|long|string)-\d+$")
                && Equals(context.Event.Idx[k], "value"));
        }
    }

    [Fact]
    public async Task CreatingField_IsForwardOnly_AndDoesNotBackfillLegacyEvents()
    {
        var legacyEvent = GenerateEvent();
        legacyEvent.Source = "custom-field-cutover";
        legacyEvent.StackId = TestConstants.StackId;
        legacyEvent.Data = new DataDictionary { ["customer_id"] = "acme" };
        legacyEvent.Idx = new DataDictionary { ["customer_id-s"] = "acme" };
        await IndexLegacyEventAsync(legacyEvent);

        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "customer_id", "keyword");
        await RefreshDataAsync();

        var currentEvent = GenerateEvent();
        currentEvent.Source = "custom-field-cutover";
        currentEvent.Data = new DataDictionary { ["customer_id"] = "acme" };
        var context = await _pipeline.RunAsync(currentEvent, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);
        await RefreshDataAsync();

        var matches = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FieldEquals(eventDocument => eventDocument.Source, "custom-field-cutover")
            .FilterExpression("data.customer_id:acme"));

        Assert.Single(matches.Documents);
        Assert.Equal(context.Event.Id, matches.Documents.Single().Id);

        var legacyResults = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FieldEquals(eventDocument => eventDocument.Id, legacyEvent.Id)
            .Include(eventDocument => eventDocument.Id, eventDocument => eventDocument.Idx));
        var reloadedLegacyEvent = Assert.Single(legacyResults.Documents);
        Assert.Equal("acme", reloadedLegacyEvent.Idx?["customer_id-s"]);
        Assert.False(reloadedLegacyEvent.Idx?.ContainsKey(definition.GetIdxName()) ?? false);
    }

    [Fact]
    public async Task OnlyConfiguredFields_AreIndexed()
    {
        // Without custom field definitions, no data is indexed to idx
        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["ispremium"] = true;
        ev.Data["count"] = 42;

        var org = GetOrganization();
        org.HasPremiumFeatures = true;

        var context = await _pipeline.RunAsync(ev, org, GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        // Without field definitions, nothing should be in idx
        if (context.Event.Idx is not null && context.Event.Idx.Count > 0)
        {
            // Only system-provisioned fields (sessionend, haserror) or explicitly configured fields should be present
            Assert.DoesNotContain(context.Event.Idx.Keys, k => k.Contains("ispremium") || k.Contains("count"));
        }
    }

    [Fact]
    public async Task TypeConversion_Boolean_Works()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "is_active", "bool");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["is_active"] = true;

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        Assert.NotNull(context.Event.Idx);
        var boolValues = context.Event.Idx.Values.OfType<bool>().ToList();
        Assert.Contains(true, boolValues);
    }

    [Fact]
    public async Task TypeConversion_Integer_Works()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "retry_count", "int");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["retry_count"] = 42;

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        Assert.NotNull(context.Event.Idx);
        var intValues = context.Event.Idx.Values.OfType<int>().ToList();
        Assert.Contains(42, intValues);
    }

    [Fact]
    public async Task InvalidValueForType_IsSkippedGracefully()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "count", "int");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["count"] = "not_a_number";

        // Should not throw - graceful handling
        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        // Event should still be saved successfully
        var savedEvent = await _eventRepository.GetByIdAsync(context.Event.Id);
        Assert.NotNull(savedEvent);
    }

    [Fact]
    public async Task MultipleOrgs_GetCorrectIndexing()
    {
        // Set up definitions for two different orgs
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "org1_field", "keyword");
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId2, "org2_field", "keyword");
        await RefreshDataAsync();

        // Event for org 1
        var ev1 = GenerateEvent();
        ev1.Data ??= new DataDictionary();
        ev1.Data["org1_field"] = "hello";
        ev1.Data["org2_field"] = "should_not_index";

        var context1 = await _pipeline.RunAsync(ev1, GetOrganization(), GetProject());
        Assert.False(context1.HasError, context1.ErrorMessage);

        Assert.NotNull(context1.Event.Idx);
        // org1_field should be indexed
        var stringValues = context1.Event.Idx.Values.OfType<string>().ToList();
        Assert.Contains("hello", stringValues);
    }

    [Fact]
    public async Task SaveAsync_CustomFieldRemovedFromData_ClearsManagedIdxSlot()
    {
        // Arrange
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "deployment_region", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data = new DataDictionary { { "deployment_region", "us-east-1" } };

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);
        Assert.NotNull(context.Event.Idx);
        Assert.Contains(definition.GetIdxName(), context.Event.Idx.Keys);

        // Act
        context.Event.Data!.Remove("deployment_region");
        await _eventRepository.SaveAsync(context.Event, o => o.ImmediateConsistency());
        await RefreshDataAsync();

        // Assert
        var reloadedEvent = await _eventRepository.GetByIdAsync(context.Event.Id);
        Assert.NotNull(reloadedEvent);
        Assert.True(reloadedEvent.Idx is null || !reloadedEvent.Idx.ContainsKey(definition.GetIdxName()));

        var results = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.deployment_region:\"us-east-1\""));

        Assert.Empty(results.Documents);
    }

    [Fact]
    public async Task FindAsync_ProjectScopedCustomFieldFilter_ResolvesTenantMapping()
    {
        // Arrange
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "environment", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data = new DataDictionary { { "environment", "production" } };

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);
        await RefreshDataAsync();

        // Act
        var results = await _eventRepository.FindAsync(q => q
            .Project(TestConstants.ProjectId)
            .FilterExpression("idx.environment:production"));

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal(context.Event.Id, results.Documents.Single().Id);
    }

    [Fact]
    public async Task FindAsync_StackScopedCustomFieldFilter_ResolvesTenantMapping()
    {
        // Arrange
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "release_channel", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data = new DataDictionary { { "release_channel", "beta" } };

        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);
        await RefreshDataAsync();

        // Act
        var results = await _eventRepository.FindAsync(q => q
            .Stack(context.Event.StackId)
            .FilterExpression("idx.release_channel:beta"));

        // Assert
        Assert.Single(results.Documents);
        Assert.Equal(context.Event.Id, results.Documents.Single().Id);
    }

    [Fact]
    public async Task FilterQuery_Keyword_Equals()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "environment", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        ev.Data ??= new DataDictionary();
        ev.Data["environment"] = "production";
        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        await RefreshDataAsync();

        var results = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.environment:production"));

        Assert.Single(results.Documents);
    }

    [Fact]
    public async Task FilterQuery_Keyword_Missing()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "environment", "keyword");
        await RefreshDataAsync();

        // Event WITH the field
        var evWith = GenerateEvent();
        evWith.Data = new DataDictionary { { "environment", "staging" } };
        var ctxWith = await _pipeline.RunAsync(evWith, GetOrganization(), GetProject());
        Assert.False(ctxWith.HasError, ctxWith.ErrorMessage);

        // Event WITHOUT the field
        var evWithout = GenerateEvent();
        evWithout.Data = new DataDictionary { { "some_other_key", "value" } };
        var ctxWithout = await _pipeline.RunAsync(evWithout, GetOrganization(), GetProject());
        Assert.False(ctxWithout.HasError, ctxWithout.ErrorMessage);

        await RefreshDataAsync();

        var missing = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("_missing_:idx.environment"));

        Assert.Single(missing.Documents);
    }

    [Fact]
    public async Task FilterQuery_Numeric_RangeOperators()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "response_ms", "double");
        await RefreshDataAsync();

        foreach (var ms in new[] { 100.0, 500.0, 1200.0 })
        {
            var ev = GenerateEvent();
            ev.Data = new DataDictionary { { "response_ms", ms } };
            var ctx = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
            Assert.False(ctx.HasError, ctx.ErrorMessage);
        }

        await RefreshDataAsync();

        var slow = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.response_ms:>500"));

        Assert.Single(slow.Documents);

        var fast = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.response_ms:<200"));

        Assert.Single(fast.Documents);
    }

    [Fact]
    public async Task FilterQuery_Bool_TrueAndFalse()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "is_premium", "bool");
        await RefreshDataAsync();

        var evTrue = GenerateEvent();
        evTrue.Data = new DataDictionary { { "is_premium", true } };
        var ctxTrue = await _pipeline.RunAsync(evTrue, GetOrganization(), GetProject());
        Assert.False(ctxTrue.HasError, ctxTrue.ErrorMessage);

        var evFalse = GenerateEvent();
        evFalse.Data = new DataDictionary { { "is_premium", false } };
        var ctxFalse = await _pipeline.RunAsync(evFalse, GetOrganization(), GetProject());
        Assert.False(ctxFalse.HasError, ctxFalse.ErrorMessage);

        await RefreshDataAsync();

        var premium = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.is_premium:true"));

        Assert.Single(premium.Documents);

        var nonPremium = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.is_premium:false"));

        Assert.Single(nonPremium.Documents);
    }

    [Fact]
    public async Task FilterQuery_MultipleCustomFields_ANDCombination()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "env", "keyword");
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "version", "keyword");
        await RefreshDataAsync();

        var evMatch = GenerateEvent();
        evMatch.Data = new DataDictionary { { "env", "prod" }, { "version", "2.0" } };
        var ctxMatch = await _pipeline.RunAsync(evMatch, GetOrganization(), GetProject());
        Assert.False(ctxMatch.HasError, ctxMatch.ErrorMessage);

        var evNoMatch = GenerateEvent();
        evNoMatch.Data = new DataDictionary { { "env", "prod" }, { "version", "1.0" } };
        var ctxNoMatch = await _pipeline.RunAsync(evNoMatch, GetOrganization(), GetProject());
        Assert.False(ctxNoMatch.HasError, ctxNoMatch.ErrorMessage);

        await RefreshDataAsync();

        var results = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FilterExpression("idx.env:prod idx.version:2.0"));

        Assert.Single(results.Documents);
    }

    [Fact]
    public async Task Event_WithPrePopulatedIdx_IsStripped_AndRecomputedFromData()
    {
        // Register "severity" as a custom field — the slot (e.g. keyword-1 or keyword-2) depends on
        // which system fields are provisioned first, so we don't hard-code the slot number.
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId, "severity", "keyword");
        await RefreshDataAsync();

        var ev = GenerateEvent();
        // Inject a bogus value into the managed slot and a different bogus value into a non-slot key.
        var injectedSlotKey = definition.GetIdxName(); // e.g. "keyword-1" or "keyword-2"
        ev.Idx = new DataDictionary
        {
            [injectedSlotKey] = "injected_via_client"
        };
        ev.Data ??= new DataDictionary();
        ev.Data["severity"] = "low";

        // Running through the pipeline fires DocumentsChanging which calls ClearCustomFieldSlots,
        // which removes all managed new-format slot keys and re-populates from ev.Data.
        var context = await _pipeline.RunAsync(ev, GetOrganization(), GetProject());
        Assert.False(context.HasError, context.ErrorMessage);

        // The in-memory event Idx must contain "low" (server-computed from data)
        // and must NOT contain the client-injected "injected_via_client" value.
        Assert.NotNull(context.Event.Idx);
        Assert.Contains(context.Event.Idx.Values, v => "low".Equals(v?.ToString()));
        Assert.DoesNotContain(context.Event.Idx.Values, v => "injected_via_client".Equals(v?.ToString()));
    }

    [Fact]
    public async Task Event_WhenSystemFieldProvisioningFails_StillStripsClientManagedSlots()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), TestConstants.OrganizationId2, "claimed_keyword_slot", "keyword");

        var eventDocument = GenerateEvent();
        eventDocument.OrganizationId = TestConstants.OrganizationId2;
        eventDocument.StackId = TestConstants.StackId;
        eventDocument.Idx = new DataDictionary
        {
            [EventCustomFieldService.SessionReferenceIdxField] = "injected_via_client",
            ["session-r"] = "legacy_server_field"
        };

        await _eventRepository.AddAsync(eventDocument, o => o.ImmediateConsistency());

        Assert.Null(eventDocument.Idx);
    }

    private Organization GetOrganization()
    {
        return _organizationData.GenerateSampleOrganization(_billingManager, _plans);
    }

    private Project GetProject()
    {
        return _projectData.GenerateSampleProject();
    }

    private PersistentEvent GenerateEvent(DateTimeOffset? occurrenceDate = null)
    {
        occurrenceDate ??= DateTimeOffset.Now;
        return _eventData.GenerateEvent(
            projectId: TestConstants.ProjectId,
            organizationId: TestConstants.OrganizationId,
            generateTags: false,
            generateData: false,
            occurrenceDate: occurrenceDate);
    }

    private async Task<long> CountSystemFieldMatchesAsync(string source, string filter)
    {
        var results = await _eventRepository.FindAsync(q => q
            .Organization(TestConstants.OrganizationId)
            .FieldEquals(eventDocument => eventDocument.Source, source)
            .FilterExpression(filter));
        return results.Total;
    }

    private async Task IndexLegacyEventAsync(PersistentEvent eventDocument)
    {
        if (String.IsNullOrEmpty(eventDocument.Id))
            eventDocument.Id = ObjectId.GenerateNewId().ToString();

        string index = _elasticConfiguration.Events.GetIndex(eventDocument);
        await _elasticConfiguration.Events.EnsureIndexAsync(eventDocument);
        var response = await _elasticConfiguration.Client.IndexAsync(eventDocument, request => request
            .Index(index)
            .Id(eventDocument.Id)
            .Refresh(Refresh.WaitFor), TestContext.Current.CancellationToken);
        Assert.True(response.IsValidResponse, response.DebugInformation);

        var stored = await _elasticConfiguration.Client.GetAsync<PersistentEvent>(eventDocument.Id, request => request
            .Index(index), TestContext.Current.CancellationToken);
        Assert.True(stored.IsValidResponse, stored.DebugInformation);
        var storedIdx = Assert.IsType<DataDictionary>(stored.Source?.Idx);
        foreach (var (key, value) in eventDocument.Idx ?? [])
        {
            if (value is DateTime expectedDate)
            {
                var storedDate = Assert.IsType<DateTimeOffset>(storedIdx[key]);
                Assert.Equal(expectedDate, storedDate.UtcDateTime);
            }
            else
            {
                Assert.Equal(value, storedIdx[key]);
            }
        }
    }

    private async Task CreateProjectDataAsync()
    {
        foreach (var organization in _organizationData.GenerateSampleOrganizations(_billingManager, _plans))
        {
            _billingManager.ApplyBillingPlan(organization, _plans.SmallPlan, _userData.GenerateSampleUser());

            if (organization.BillingPrice > 0)
            {
                organization.StripeCustomerId = "stripe_customer_id";
                organization.CardLast4 = "1234";
                organization.SubscribeDate = DateTime.UtcNow;
                organization.BillingChangeDate = DateTime.UtcNow;
                organization.BillingChangedByUserId = TestConstants.UserId;
            }

            await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        }

        await _projectRepository.AddAsync(_projectData.GenerateSampleProjects(), o => o.ImmediateConsistency().Cache());

        foreach (var user in _userData.GenerateSampleUsers())
        {
            if (user.Id == TestConstants.UserId)
            {
                user.OrganizationIds.Add(TestConstants.OrganizationId2);
                user.OrganizationIds.Add(TestConstants.OrganizationId3);
            }

            if (!user.IsEmailAddressVerified)
                user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

            await GetService<IUserRepository>().AddAsync(user, o => o.ImmediateConsistency().Cache());
        }
    }
}
