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

namespace Exceptionless.Tests.CustomFields;

public sealed class CustomFieldEndpointContractTests : IntegrationTestsBase
{
    private readonly ICustomFieldDefinitionRepository _definitionRepository;

    public CustomFieldEndpointContractTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _definitionRepository = GetService<ICustomFieldDefinitionRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
        await GetService<EventCustomFieldService>().EnsureSystemFieldsAsync(SampleDataService.TEST_ORG_ID);
    }

    [Fact]
    public async Task Patch_OmittedDescriptionPreservesIt_AndExplicitNullClearsIt()
    {
        var definition = await _definitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "customer_id", "keyword", "Customer identifier");

        await SendRequestAsync(request => request
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .Content("""{"display_order":9}""", "application/json")
            .StatusCodeShouldBeOk());

        var preserved = await _definitionRepository.GetByIdAsync(definition.Id);
        Assert.Equal("Customer identifier", preserved!.Description);
        Assert.Equal(9, preserved.DisplayOrder);

        await SendRequestAsync(request => request
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .Content("""{"description":null}""", "application/json")
            .StatusCodeShouldBeOk());

        var cleared = await _definitionRepository.GetByIdAsync(definition.Id);
        Assert.Null(cleared!.Description);
    }

    [Fact]
    public async Task Get_SortsByDisplayOrderThenName()
    {
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "zeta", "keyword", displayOrder: 2);
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "beta", "keyword", displayOrder: 1);
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "alpha", "keyword", displayOrder: 1);

        var fields = await SendRequestAsAsync<List<CustomFieldDefinitionResponse>>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .StatusCodeShouldBeOk());

        Assert.Equal(["alpha", "beta", "zeta"], fields!.Select(field => field.Name));
    }

    [Fact]
    public async Task Delete_QuotedFieldTextDoesNotCountAsReference_AndReturnsNoContent()
    {
        var definition = await _definitionRepository.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "customer_id", "keyword");
        await GetService<ISavedViewRepository>().AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            CreatedByUserId = TestConstants.UserId,
            Filter = "message:\"idx.customer_id:foo\"",
            Name = "Quoted text",
            Slug = "quoted-text",
            ViewType = "events"
        });

        await SendRequestAsync(request => request
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields", definition.Id)
            .StatusCodeShouldBeNoContent());
    }

    [Fact]
    public async Task EventCount_UnknownCustomField_ReturnsStableBadRequestIdentifier()
    {
        var problem = await SendRequestAsAsync<ValidationProblemDetails>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "count")
            .QueryString("filter", "idx.unknown_customer_field:value")
            .StatusCodeShouldBeBadRequest());

        Assert.NotNull(problem);
        Assert.Contains(EventCustomFieldQueryPolicy.UnknownFilterField, problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateSavedView_UnknownCustomField_ReturnsStableBadRequestIdentifier()
    {
        var problem = await SendRequestAsAsync<ValidationProblemDetails>(request => request
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Unknown custom field",
                ViewType = "events",
                Filter = "idx.unknown_customer_field:value"
            })
            .StatusCodeShouldBeBadRequest());

        Assert.NotNull(problem);
        Assert.Contains(EventCustomFieldQueryPolicy.UnknownFilterField, problem.Errors.Keys);
    }

    [Theory]
    [InlineData("idx.customer_id:value", null)]
    [InlineData("type:error", "[{\"type\":\"string\",\"term\":\"idx.customer_id\",\"value\":\"value\"}]")]
    [InlineData("type:error", "[{\"type\":\"keyword\",\"value\":\"idx.customer_id:value\"}]")]
    public async Task PromotePredefinedView_OrganizationCustomField_ReturnsValidationError(string filter, string? filterDefinitions)
    {
        var savedView = await GetService<ISavedViewRepository>().AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            CreatedByUserId = TestConstants.UserId,
            Name = "Organization-specific filter",
            Slug = "organization-specific-filter",
            ViewType = "events",
            Filter = filter,
            FilterDefinitions = filterDefinitions,
            Version = 1
        }, options => options.ImmediateConsistency());

        var problem = await SendRequestAsAsync<ValidationProblemDetails>(request => request
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("saved-views", savedView.Id, "predefined")
            .ExpectedStatus(System.Net.HttpStatusCode.UnprocessableEntity));

        Assert.NotNull(problem);
        Assert.Contains("filter", problem.Errors.Keys);
    }
}
