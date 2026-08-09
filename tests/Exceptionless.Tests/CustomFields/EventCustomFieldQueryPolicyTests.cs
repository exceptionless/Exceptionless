using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Xunit;

namespace Exceptionless.Tests.CustomFields;

public sealed class EventCustomFieldQueryPolicyTests : IntegrationTestsBase
{
    private readonly ICustomFieldDefinitionRepository _definitionRepository;
    private readonly EventCustomFieldQueryPolicy _policy;

    public EventCustomFieldQueryPolicyTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _definitionRepository = GetService<ICustomFieldDefinitionRepository>();
        _policy = GetService<EventCustomFieldQueryPolicy>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task ValidateAsync_ActiveDefinition_AllowsDataAndIdxLogicalNames()
    {
        await _definitionRepository.AddFieldAsync(nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "customer_id", "keyword");
        var appFilter = new AppFilter(await GetOrganizationAsync(SampleDataService.TEST_ORG_ID));

        var result = await _policy.ValidateAsync(
            ["data.customer_id", "idx.customer_id"], appFilter, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("data.unknown")]
    [InlineData("idx.unknown")]
    [InlineData("idx.keyword-7")]
    public async Task ValidateAsync_UnknownOrRawField_ReturnsStableError(string field)
    {
        var appFilter = new AppFilter(await GetOrganizationAsync(SampleDataService.TEST_ORG_ID));

        var result = await _policy.ValidateAsync([field], appFilter, TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(EventCustomFieldQueryPolicy.UnknownFilterField, result.ErrorCode);
        Assert.Equal(field, result.Field);
    }

    [Fact]
    public async Task ValidateAsync_MultipleOrganizations_ReturnsScopeRequired()
    {
        var organizations = new[]
        {
            await GetOrganizationAsync(SampleDataService.TEST_ORG_ID),
            await GetOrganizationAsync(SampleDataService.FREE_ORG_ID)
        };

        var result = await _policy.ValidateAsync(
            ["data.customer_id"], new AppFilter(organizations), TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(EventCustomFieldQueryPolicy.CustomFieldScopeRequired, result.ErrorCode);
    }

    [Theory]
    [InlineData("data.@version")]
    [InlineData("data.sessionend")]
    [InlineData("data.haserror")]
    [InlineData("ref.session")]
    public async Task ValidateAsync_BuiltInAndSystemFields_DoNotRequireDefinition(string field)
    {
        var result = await _policy.ValidateAsync([field], appFilter: null, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private async Task<Organization> GetOrganizationAsync(string organizationId)
        => await GetService<Exceptionless.Core.Repositories.IOrganizationRepository>().GetByIdAsync(organizationId)
            ?? throw new InvalidOperationException($"Organization '{organizationId}' was not found.");
}
