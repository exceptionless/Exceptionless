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
    public async Task DataSeedStartupAction_PendingMigration_DoesNotReadLegacySavedViews()
    {
        // Arrange
        const string savedViewId = "770000000000000000000095";
        var source = JsonNode.Parse(
            $$"""
            {
              "id": "{{savedViewId}}",
              "organization_id": "{{PredefinedSavedViewsDataSeed.SystemOrganizationId}}",
              "created_by_user_id": "{{PredefinedSavedViewsDataSeed.SystemUserId}}",
              "name": "Legacy Predefined View",
              "slug": "legacy-predefined-view",
              "view_type": "events",
              "columns": {
                "level": true
              },
              "version": 1,
              "created_utc": "2026-01-01T00:00:00Z",
              "updated_utc": "2026-01-01T00:00:00Z"
            }
            """
        )!.AsObject();

        var migrationStateRepository = GetService<IMigrationStateRepository>();
        await migrationStateRepository.AddAsync(new MigrationState
        {
            Id = "3",
            Version = 3,
            MigrationType = MigrationType.Versioned,
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow
        });

        var indexResponse = await _client.IndexAsync(
            source,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(savedViewId)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        // Act
        await GetService<DataSeedService>().RunAsync(TestCancellationToken);

        // Assert
        var getResponse = await _client.GetAsync<JsonElement>(
            savedViewId,
            request => request.Index(_configuration.SavedViews.VersionedName),
            TestCancellationToken);
        Assert.True(getResponse.IsValidResponse);
        Assert.Equal(JsonValueKind.True, getResponse.Source.GetProperty("columns").GetProperty("level").ValueKind);
    }

    [Fact]
    public async Task DataSeedStartupAction_OnlyRepeatableMigrationsPending_SeedsData()
    {
        // Arrange
        var migrationStateRepository = GetService<IMigrationStateRepository>();
        await migrationStateRepository.AddAsync(new MigrationState
        {
            Id = "4",
            Version = 4,
            MigrationType = MigrationType.VersionedAndResumable,
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow
        });

        // Act
        await GetService<DataSeedService>().RunAsync(TestCancellationToken);

        // Assert
        var savedViews = await _repository.GetByOrganizationIdAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            o => o.ImmediateConsistency());
        Assert.NotEmpty(savedViews.Documents);
    }

    [Fact]
    public async Task RunAsync_LegacySavedView_ReplacesDocumentWithStructuredColumns()
    {
        // Arrange
        const string savedViewId = "770000000000000000000099";
        const string customizedSavedViewId = "770000000000000000000098";
        const string predefinedWithoutColumnsId = "770000000000000000000096";
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
        var predefinedWithoutColumnsSource = source.DeepClone().AsObject();
        predefinedWithoutColumnsSource["id"] = predefinedWithoutColumnsId;
        predefinedWithoutColumnsSource["name"] = "Predefined Without Columns";
        predefinedWithoutColumnsSource.Remove("columns");
        predefinedWithoutColumnsSource.Remove("column_order");
        predefinedWithoutColumnsSource["predefined_content_hash"] = MigrateSavedViewColumns.GetLegacyContentHash(predefinedWithoutColumnsSource);

        var indexResponse = await _client.IndexAsync(
            source,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(savedViewId),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        indexResponse = await _client.IndexAsync(
            predefinedWithoutColumnsSource,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(predefinedWithoutColumnsId),
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

        var predefinedWithoutColumns = await _repository.GetByIdAsync(predefinedWithoutColumnsId, o => o.ImmediateConsistency());
        Assert.NotNull(predefinedWithoutColumns);
        Assert.Null(predefinedWithoutColumns.Columns);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(predefinedWithoutColumns), predefinedWithoutColumns.PredefinedContentHash);

        var getResponse = await _client.GetAsync<JsonElement>(
            savedViewId,
            request => request.Index(_configuration.SavedViews.VersionedName),
            TestCancellationToken);
        Assert.True(getResponse.IsValidResponse);
        Assert.False(getResponse.Source.TryGetProperty("column_order", out _));
        Assert.Equal(JsonValueKind.Object, getResponse.Source.GetProperty("columns").GetProperty("project").ValueKind);
    }

    [Fact]
    public async Task IndexMigratedDocumentAsync_ConcurrentUpdate_DoesNotOverwriteDocument()
    {
        // Arrange
        const string savedViewId = "770000000000000000000097";
        var source = JsonNode.Parse(
            $$"""
            {
              "id": "{{savedViewId}}",
              "name": "Legacy Columns",
              "columns": {
                "project": true
              }
            }
            """
        )!.AsObject();

        var indexResponse = await _client.IndexAsync(
            source,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(savedViewId)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        var staleDocument = await _client.GetAsync<JsonElement>(
            savedViewId,
            request => request.Index(_configuration.SavedViews.VersionedName),
            TestCancellationToken);
        Assert.True(staleDocument.IsValidResponse);
        Assert.NotNull(staleDocument.SeqNo);
        Assert.NotNull(staleDocument.PrimaryTerm);

        var concurrentSource = source.DeepClone().AsObject();
        concurrentSource["name"] = "Edited During Migration";
        indexResponse = await _client.IndexAsync(
            concurrentSource,
            request => request
                .Index(_configuration.SavedViews.VersionedName)
                .Id(savedViewId)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(indexResponse.IsValidResponse);

        Assert.True(MigrateSavedViewColumns.TryMigrate(source));
        var migration = GetService<MigrateSavedViewColumns>();

        // Act
        indexResponse = await migration.IndexMigratedDocumentAsync(
            source,
            savedViewId,
            staleDocument.SeqNo.Value,
            staleDocument.PrimaryTerm.Value,
            TestCancellationToken);

        // Assert
        Assert.False(indexResponse.IsValidResponse);
        Assert.Equal(409, indexResponse.ApiCallDetails.HttpStatusCode);

        var currentDocument = await _client.GetAsync<JsonElement>(
            savedViewId,
            request => request.Index(_configuration.SavedViews.VersionedName),
            TestCancellationToken);
        Assert.True(currentDocument.IsValidResponse);
        Assert.Equal("Edited During Migration", currentDocument.Source.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.True, currentDocument.Source.GetProperty("columns").GetProperty("project").ValueKind);
    }
}
