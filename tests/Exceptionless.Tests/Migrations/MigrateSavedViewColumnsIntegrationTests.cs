using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Seed;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MigrateSavedViewColumnsIntegrationTests : IntegrationTestsBase
{
    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly ISavedViewRepository _repository;

    public MigrateSavedViewColumnsIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _configuration = GetService<ExceptionlessElasticConfiguration>();
        _client = _configuration.Client;
        _repository = GetService<ISavedViewRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<MigrateSavedViewColumns>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_LegacySavedView_ReplacesDocumentWithStructuredColumns()
    {
        // Arrange
        const string savedViewId = "770000000000000000000099";
        const string customizedSavedViewId = "770000000000000000000098";
        var source = JsonNode.Parse(
            $$"""
            {
              "id": "{{savedViewId}}",
              "organization_id": "550000000000000000000001",
              "created_by_user_id": "660000000000000000000001",
              "name": "Legacy Columns",
              "slug": "legacy-columns",
              "view_type": "events",
              "columns": {
                "project": true,
                "summary": false
              },
              "column_order": [
                "summary",
                "project"
              ],
              "version": 1,
              "created_utc": "2026-01-01T00:00:00Z",
              "updated_utc": "2026-01-01T00:00:00Z"
            }
            """
        )!.AsObject();
        source["predefined_content_hash"] = MigrateSavedViewColumns.GetLegacyContentHash(source);
        var customizedSource = source.DeepClone().AsObject();
        customizedSource["id"] = customizedSavedViewId;
        customizedSource["name"] = "Customized Legacy Columns";
        customizedSource["predefined_content_hash"] = "customized-content-hash";

        var indexResponse = await _client.IndexAsync(
            source,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(savedViewId),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        indexResponse = await _client.IndexAsync(
            customizedSource,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(customizedSavedViewId)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        // Act
        var migration = GetService<MigrateSavedViewColumns>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);

        // Assert
        var savedView = await _repository.GetByIdAsync(savedViewId, o => o.ImmediateConsistency());
        Assert.NotNull(savedView);
        Assert.False(savedView.Columns?["summary"].Visible);
        Assert.Equal(0, savedView.Columns?["summary"].Position);
        Assert.True(savedView.Columns?["project"].Visible);
        Assert.Equal(1, savedView.Columns?["project"].Position);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(savedView), savedView.PredefinedContentHash);

        var customizedSavedView = await _repository.GetByIdAsync(customizedSavedViewId, o => o.ImmediateConsistency());
        Assert.NotNull(customizedSavedView);
        Assert.Equal("customized-content-hash", customizedSavedView.PredefinedContentHash);
        Assert.Equal(1, customizedSavedView.Columns?["project"].Position);

        var getResponse = await _client.GetAsync<JsonElement>(
            savedViewId,
            request => request.Index(_configuration.SavedViews.VersionedName),
            TestCancellationToken);
        Assert.True(getResponse.IsValidResponse);
        Assert.False(getResponse.Source.TryGetProperty("column_order", out _));
        Assert.Equal(JsonValueKind.Object, getResponse.Source.GetProperty("columns").GetProperty("project").ValueKind);
    }
}
