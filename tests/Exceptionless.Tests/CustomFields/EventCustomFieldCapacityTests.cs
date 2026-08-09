using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.CustomFields;

public sealed class EventCustomFieldCapacityTests : IntegrationTestsBase
{
    private readonly ICustomFieldDefinitionRepository _definitionRepository;
    private readonly EventCustomFieldService _service;

    public EventCustomFieldCapacityTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _definitionRepository = GetService<ICustomFieldDefinitionRepository>();
        _service = GetService<EventCustomFieldService>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task CreateFieldAsync_SoftDeletedDefinition_ConsumesLifetimeCapacity()
    {
        var first = await _service.CreateFieldAsync(
            SampleDataService.TEST_ORG_ID, "first", "keyword", 20, 1,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(EventCustomFieldService.CreateFieldStatus.Created, first.Status);

        first.Definition!.IsDeleted = true;
        await _definitionRepository.SaveAsync(first.Definition);

        var second = await _service.CreateFieldAsync(
            SampleDataService.TEST_ORG_ID, "second", "keyword", 20, 1,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EventCustomFieldService.CreateFieldStatus.LifetimeLimitReached, second.Status);
    }

    [Fact]
    public async Task CreateFieldAsync_ExistingOverLimitDefinitionsRemainReadableButCannotAllocate()
    {
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "legacy_one", "keyword");
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "legacy_two", "keyword");

        var result = await _service.CreateFieldAsync(
            SampleDataService.TEST_ORG_ID, "new_field", "keyword", 20, 1,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EventCustomFieldService.CreateFieldStatus.LifetimeLimitReached, result.Status);
        var mapping = await _definitionRepository.GetFieldMappingAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID);
        Assert.Contains("legacy_one", mapping.Keys);
        Assert.Contains("legacy_two", mapping.Keys);
    }

    [Fact]
    public async Task PostField_LifetimeCapacityReached_ReturnsStableValidationIdentifier()
    {
        for (int index = 0; index < 20; index++)
        {
            var definition = await _definitionRepository.AddFieldAsync(
                nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, $"retired_{index}", "keyword");
            definition.IsDeleted = true;
            await _definitionRepository.SaveAsync(definition);
        }

        var problem = await SendRequestAsAsync<ValidationProblemDetails>(request => request
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "event-custom-fields")
            .Content(new NewCustomFieldDefinition { Name = "next_field", IndexType = "keyword" })
            .StatusCodeShouldBeUnprocessableEntity());

        Assert.NotNull(problem);
        Assert.Contains("custom_field_lifetime_limit", problem.Errors.Keys);
    }
}
