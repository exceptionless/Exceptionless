using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using DataDictionary = Exceptionless.Core.Models.DataDictionary;

namespace Exceptionless.Tests.CustomFields;

public sealed class CustomFieldApiTests : IntegrationTestsBase
{
    private readonly ICustomFieldDefinitionRepository _customFieldDefinitionRepository;
    private readonly ISavedViewRepository _savedViewRepository;

    public CustomFieldApiTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _customFieldDefinitionRepository = GetService<ICustomFieldDefinitionRepository>();
        _savedViewRepository = GetService<ISavedViewRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task GetFields_ReturnsEmptyList_ForNewOrganization()
    {
        var fields = await SendRequestAsAsync<List<CustomFieldDefinitionResponse>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(fields);
        Assert.Empty(fields);
    }

    [Fact]
    public async Task PostField_CreatesCustomFieldDefinition()
    {
        var newField = new NewCustomFieldDefinition
        {
            Name = "response_time",
            IndexType = "double",
            Description = "API response time in ms",
            DisplayOrder = 1
        };

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(newField)
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        // Verify the user field was persisted (system fields are also auto-provisioned but excluded here)
        var results = await _customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID);
        var userField = results.Documents.FirstOrDefault(f => f.Name == "response_time");
        Assert.NotNull(userField);
        Assert.Equal("double", userField.IndexType);
        Assert.Equal("API response time in ms", userField.Description);
        Assert.Equal(1, userField.DisplayOrder);

        // System fields are provisioned first, so the user field should NOT be in slot 1 for its type
        Assert.True(results.Documents.Count >= 3, "Expected system fields + user field");
        Assert.True(results.Documents.All(f => !EventCustomFieldService.IsSystemField(f.Name) || f.IndexSlot == 1),
            "System fields should always occupy slot 1");
    }

    [Fact]
    public Task PostField_RejectInvalidFieldName_StartsWithAt()
    {
        var newField = new NewCustomFieldDefinition
        {
            Name = "@invalid",
            IndexType = "keyword"
        };

        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(newField)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostField_AllowsSensitiveLookingFieldName()
    {
        var newField = new NewCustomFieldDefinition
        {
            Name = "user_password",
            IndexType = "keyword"
        };

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(newField)
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        var results = await _customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID);
        Assert.Contains(results.Documents, d => d.Name == "user_password");
    }

    [Fact]
    public Task PostField_RejectEmptyName()
    {
        var newField = new NewCustomFieldDefinition
        {
            Name = "",
            IndexType = "keyword"
        };

        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(newField)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostField_RejectEmptyIndexType()
    {
        var newField = new NewCustomFieldDefinition
        {
            Name = "valid_field",
            IndexType = ""
        };

        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(newField)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostField_RejectsUnknownIndexType()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition
            {
                Name = "valid_field",
                IndexType = "this_is_invalid"
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostField_RejectWhenMaxFieldsReached()
    {
        // Create max fields directly via repository (default is 20)
        for (int i = 0; i < 20; i++)
        {
            await _customFieldDefinitionRepository.AddFieldAsync(
                nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, $"field_{i}", "keyword");
        }

        // The 21st via API should be rejected
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "field_overflow", IndexType = "keyword" })
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task GetFields_ReturnsCreatedFields()
    {
        // Create a field directly via repository
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "duration", "double", "Duration");

        await RefreshDataAsync();

        var fields = await SendRequestAsAsync<List<CustomFieldDefinitionResponse>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(fields);
        Assert.Single(fields);
        Assert.Equal("duration", fields[0].Name);
        Assert.Equal("double", fields[0].IndexType);
    }

    [Fact]
    public async Task PatchField_UpdatesDescription()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "status", "keyword", "Original");

        var updated = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .Content(new UpdateCustomFieldDefinition { Description = "Updated description" })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.Equal("Updated description", updated.Description);
    }

    [Fact]
    public async Task PatchField_UpdatesDisplayOrder()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "priority", "integer", displayOrder: 1);

        var updated = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .Content(new UpdateCustomFieldDefinition { DisplayOrder = 5 })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.Equal(5, updated.DisplayOrder);
    }

    [Fact]
    public async Task DeleteField_SoftDeletesField_AndHidesFromList()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "temp_field", "keyword");

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Verify the field no longer appears in the active list (soft-deleted)
        var fields = await SendRequestAsAsync<List<CustomFieldDefinitionResponse>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(fields);
        Assert.Empty(fields);
    }

    [Fact]
    public Task DeleteField_ReturnsNotFound_ForNonExistentField()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", "nonexistent123456789012")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PostField_RejectDuplicateName()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "duplicate_field", "keyword");

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "duplicate_field", IndexType = "keyword" })
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostField_RejectDuplicateName_CaseInsensitive()
    {
        await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "MyField", "keyword");

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "myfield", IndexType = "keyword" })
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task DeleteField_ReturnsNotFound_WhenAlreadyQueuedForDeletion()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "idempotent_field", "keyword");

        // First delete — soft-deletes the field
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Second delete — field is now hidden from queries (soft-deleted), returns 404.
        // This is correct REST semantics: the active resource no longer exists.
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PatchField_ClearsDescription_WhenEmptyStringProvided()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "clearable_field", "keyword", "Original description");

        var updated = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .Content(new UpdateCustomFieldDefinition { Description = "" })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task DeleteField_ReturnsConflict_WithProblemDetails()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "problem_field", "keyword");

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "PD conflict view",
            Slug = "pd-conflict-view",
            ViewType = "events",
            Filter = "idx.problem_field:value",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        var problem = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );

        Assert.NotNull(problem?.Detail);
        Assert.Contains("problem_field", problem!.Detail);
    }

    [Fact]
    public async Task DeleteField_ReturnsConflict_WhenFieldUsedInSavedViewFilter()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "customer_id", "keyword");

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Field usage view",
            Slug = "field-usage-view",
            ViewType = "events",
            Filter = "idx.customer_id:cust_123",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task GetFields_ExcludesSystemFields()
    {
        // Trigger system field provisioning by posting a user field
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "duration_ms", IndexType = "double" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        var fields = await SendRequestAsAsync<List<CustomFieldDefinitionResponse>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(fields);
        // System fields (sessionend, haserror) must NOT appear in the API response
        Assert.DoesNotContain(fields, f => EventCustomFieldService.IsSystemField(f.Name));
        // The user field must be present
        Assert.Contains(fields, f => f.Name == "duration_ms");
    }

    [Fact]
    public Task PostField_Returns426_ForFreePlanOrganization()
    {
        // The FREE_ORG has no premium features, so adding a custom field must be rejected.
        return SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "test_field", IndexType = "keyword" })
            .ExpectedStatus((System.Net.HttpStatusCode)426)
        );
    }

    [Fact]
    public async Task PatchField_Returns426_ForFreePlanOrganization()
    {
        // Provision a field in the free org via the repository (bypassing the API gate).
        var field = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.FREE_ORG_ID, "gated_patch_field", "keyword");

        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields", field.Id)
            .Content(new UpdateCustomFieldDefinition { Description = "updated" })
            .ExpectedStatus((System.Net.HttpStatusCode)426)
        );
    }

    [Fact]
    public async Task DeleteField_Returns426_ForFreePlanOrganization()
    {
        // Provision a field in the free org via the repository (bypassing the API gate).
        var field = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.FREE_ORG_ID, "gated_delete_field", "keyword");

        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields", field.Id)
            .ExpectedStatus((System.Net.HttpStatusCode)426)
        );
    }

    [Fact]
    public async Task DeleteField_Returns400_ForSystemField()
    {
        // Provision system fields by posting any user field, then retrieve system field id
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "trigger_field", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        await RefreshDataAsync();

        var allFields = await _customFieldDefinitionRepository.FindByTenantAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID);
        var sessionEndField = allFields.Documents.FirstOrDefault(f => f.Name == "sessionend");
        Assert.NotNull(sessionEndField);

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", sessionEndField.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.BadRequest)
        );
    }

    [Fact]
    public Task GetFields_ReturnsNotFound_ForAnotherOrganization()
    {
        // A user authenticated against TEST_ORG cannot read FREE_ORG's custom fields.
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task PostField_ReturnsNotFound_ForAnotherOrganization()
    {
        // A user in TEST_ORG cannot create custom fields in FREE_ORG.
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "idor_field", IndexType = "keyword" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteField_ReturnsNotFound_ForAnotherOrganization()
    {
        // Create a field in FREE_ORG directly, then attempt to delete it as a TEST_ORG user.
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.FREE_ORG_ID, "secret_field", "keyword");

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteField_ReturnsConflict_WhenFieldInComplexFilter()
    {
        // Parenthesized or boolean-combined filter expressions must also be checked.
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "env", "keyword");

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Complex filter view",
            Slug = "complex-filter-view",
            ViewType = "events",
            Filter = "(type:error AND idx.env:production)",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task DeleteField_ReturnsConflict_WhenFieldInMissingFilter()
    {
        // Missing-value filters (_missing_:idx.fieldname) must also block deletion.
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "deploy_version", "keyword");

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Missing filter view",
            Slug = "missing-filter-view",
            ViewType = "events",
            Filter = "_missing_:idx.deploy_version",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task DeleteField_Succeeds_WhenFieldRemovedFromAllSavedViews()
    {
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "region", "keyword");

        var view = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Region view",
            Slug = "region-view",
            ViewType = "events",
            Filter = "idx.region:us-east-1",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        // First delete attempt is blocked.
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );

        // Remove field from the saved view filter.
        view.Filter = "type:error";
        await _savedViewRepository.SaveAsync(view, o => o.ImmediateConsistency());

        // Second delete attempt succeeds.
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeAccepted()
        );
    }

    [Fact]
    public async Task DeleteField_NotBlockedBySavedViewInDifferentOrganization()
    {
        // A saved view in FREE_ORG referencing "shared_field" must NOT block deletion in TEST_ORG.
        var definition = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "shared_field", "keyword");

        // Saved view belongs to FREE_ORG, not TEST_ORG.
        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.FREE_ORG_ID,
            Name = "Other org view",
            Slug = "other-org-view",
            ViewType = "events",
            Filter = "idx.shared_field:value",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        // Deletion in TEST_ORG must succeed; the other org's saved view is irrelevant.
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeAccepted()
        );
    }

    [Fact]
    public async Task PostField_QuotaCountsOnlyActiveFields_SoftDeletedDoNotCount()
    {
        // Fill up to max-1 fields directly.
        int maxFields = 20;
        for (int i = 0; i < maxFields - 1; i++)
            await _customFieldDefinitionRepository.AddFieldAsync(
                nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, $"field_{i}", "keyword");

        // Create the final (20th) field via API — this should succeed.
        var lastField = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "last_field", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );
        Assert.NotNull(lastField);

        // Now soft-delete the last field via the API.
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", lastField.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Quota now has room for one more active field (soft-deleted do not count).
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "replacement_field", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );
    }

    [Fact]
    public async Task PostField_AllowsSameNameAfterHardDelete()
    {
        // Ensure system fields are provisioned first (they occupy deterministic slots).
        var eventCustomFieldService = GetService<EventCustomFieldService>();
        await eventCustomFieldService.EnsureSystemFieldsAsync(SampleDataService.TEST_ORG_ID);

        // After a field is hard-deleted (slot freed), the same name can be reused.
        var original = await _customFieldDefinitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "reusable_field", "keyword");
        int originalSlot = original.IndexSlot;

        // Hard-delete directly through repository (bypasses the soft-delete work-item cycle).
        await _customFieldDefinitionRepository.RemoveAsync(original, o => o.ImmediateConsistency());

        // Creating a field with the same name must succeed and get a new (recycled) slot.
        var replacement = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "reusable_field", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        Assert.NotNull(replacement);
        Assert.Equal("reusable_field", replacement.Name);

        // IndexSlot is not exposed via API (security), so verify recycling via repository.
        var repoField = await _customFieldDefinitionRepository.GetByIdAsync(replacement.Id);
        Assert.NotNull(repoField);
        Assert.Equal(originalSlot, repoField.IndexSlot);
    }

    [Fact]
    public async Task PostField_NormalizesIndexTypeToLowercase()
    {
        var created = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "typed_field", IndexType = "KEYWORD" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );

        Assert.NotNull(created);
        // Must be stored as lowercase so ConvertValue's switch arms match correctly
        Assert.Equal("keyword", created.IndexType);
    }

    [Fact]
    public async Task PatchField_Returns404_ForSoftDeletedField()
    {
        // Create and immediately soft-delete a field
        var created = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "transient_field", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );
        Assert.NotNull(created);

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", created.Id)
            .StatusCodeShouldBeAccepted()
        );

        // PATCH on a soft-deleted field must return 404
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", created.Id)
            .Content(new UpdateCustomFieldDefinition { Description = "should not work" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteField_Returns404_WhenAlreadySoftDeleted()
    {
        var created = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "to_delete_twice", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );
        Assert.NotNull(created);

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", created.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Second delete must return 404, not 200 or 409
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", created.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteField_ReturnsConflict_WhenFieldInDataPrefixFilter()
    {
        // Create and register a field, then save a view using the raw data.fieldname path
        var created = await SendRequestAsAsync<CustomFieldDefinitionResponse>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "deploy_env", IndexType = "keyword" })
            .ExpectedStatus(System.Net.HttpStatusCode.Created)
        );
        Assert.NotNull(created);

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Data path view",
            Slug = "data-path-view",
            ViewType = "events",
            Filter = "data.deploy_env:production",
            Version = 1,
            CreatedByUserId = TestConstants.UserId,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", created.Id)
            .ExpectedStatus(System.Net.HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public Task PostField_RejectsUnicodeFieldName()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "fäld", IndexType = "keyword" })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchField_RejectsSystemField()
    {
        // Ensure system fields are provisioned
        var service = GetService<EventCustomFieldService>();
        await service.EnsureSystemFieldsAsync(SampleDataService.TEST_ORG_ID);

        // System fields are hidden from the GET list endpoint; retrieve directly from the repository
        var fieldMapping = await _customFieldDefinitionRepository.GetFieldMappingAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID);
        Assert.True(fieldMapping.TryGetValue(Event.KnownDataKeys.SessionEnd, out var systemField));
        Assert.NotNull(systemField);

        var problem = await SendRequestAsAsync<ProblemDetails>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", systemField.Id)
            .Content(new UpdateCustomFieldDefinition { Description = "hacked" })
            .ExpectedStatus(System.Net.HttpStatusCode.BadRequest)
        );

        Assert.NotNull(problem);
        Assert.Contains("reserved system field", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostField_ConcurrentCreation_AtQuotaLimit_OnlyOneSucceeds()
    {
        const int maxFields = 3;
        var service = GetService<EventCustomFieldService>();

        // Fill up to maxFields - 1
        for (int i = 0; i < maxFields - 1; i++)
        {
            var result = await service.CreateFieldAsync(
                SampleDataService.TEST_ORG_ID, $"field_{i}", "keyword", maxFields,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(result);
        }

        // Fire two concurrent creates for the final slot
        var task1 = service.CreateFieldAsync(SampleDataService.TEST_ORG_ID, "race_a", "keyword", maxFields,
            cancellationToken: TestContext.Current.CancellationToken);
        var task2 = service.CreateFieldAsync(SampleDataService.TEST_ORG_ID, "race_b", "keyword", maxFields,
            cancellationToken: TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(task1, task2);
        var succeeded = results.Count(r => r is not null);

        // Exactly one must succeed; quota must not be exceeded
        Assert.Equal(1, succeeded);
    }

    [Fact]
    public void UpdateSessionStart_SetsHasError_WhenTrue()
    {
        var ev = new PersistentEvent
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Type = Event.KnownTypes.Session,
            Date = DateTimeOffset.UtcNow,
            Data = new DataDictionary()
        };

        var now = DateTime.UtcNow;

        // hasError = true should add SessionHasError
        var updated = ev.UpdateSessionStart(now, hasError: true);
        Assert.True(updated);
        Assert.True(ev.Data.ContainsKey(Event.KnownDataKeys.SessionHasError));
        Assert.Equal(true, ev.Data[Event.KnownDataKeys.SessionHasError]);

        // hasError = false should remove it
        ev.UpdateSessionStart(now, hasError: false);
        Assert.False(ev.Data.ContainsKey(Event.KnownDataKeys.SessionHasError));
    }
}
