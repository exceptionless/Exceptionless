using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class AddEventTelemetryMappingsTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    [Fact]
    public async Task RunAsync_RetainedPartitions_AddsMappingsWithoutChangingExistingEvents()
    {
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        var client = configuration.Client;
        string index = $"{configuration.Events.Name}-v1-2000.01.01";
        var created = await client.Indices.CreateAsync(index, d => d.Mappings(m => m
            .Dynamic(DynamicMapping.False).Properties(p => p.Keyword("message"))), TestCancellationToken);
        Assert.True(created.IsValidResponse, created.DebugInformation);

        try
        {
            var legacy = JsonDocument.Parse("""{"message":"retained event","data":{"measurements":"legacy"}}""").RootElement;
            var indexed = await client.IndexAsync(legacy, d => d.Index(index).Id("legacy").Refresh(Refresh.WaitFor), TestCancellationToken);
            Assert.True(indexed.IsValidResponse, indexed.DebugInformation);
            var migration = GetService<IEnumerable<IMigration>>().Single(m => m is AddEventTelemetryMappings);
            var context = new MigrationContext(EmptyLock.Empty, _logger, TestCancellationToken);

            await migration.RunAsync(context);
            await migration.RunAsync(context);

            var mapping = await client.Indices.GetMappingAsync(d => d.Indices(index), TestCancellationToken);
            Assert.True(mapping.IsValidResponse, mapping.DebugInformation);
            var properties = Assert.Single(mapping.Mappings).Value.Mappings.Properties!;
            var measurements = Assert.IsType<NestedProperty>(properties["measurements"]);
            Assert.IsType<DoubleNumberProperty>(measurements.Properties!["value"]);
            Assert.IsType<FlattenedProperty>(properties["dimensions"]);
            Assert.IsType<KeywordProperty>(properties["parent_reference_id"]);
            var retained = await client.GetAsync<JsonElement>("legacy", d => d.Index(index), TestCancellationToken);
            Assert.Equal("retained event", retained.Source.GetProperty("message").GetString());
            Assert.Equal("legacy", retained.Source.GetProperty("data").GetProperty("measurements").GetString());

            var observation = JsonDocument.Parse("""{"measurements":[{"name":"duration","value":0,"unit":"ms"}],"dimensions":{"version":"4.90"}}""").RootElement;
            indexed = await client.IndexAsync(observation, d => d.Index(index).Id("native").Refresh(Refresh.WaitFor), TestCancellationToken);
            Assert.True(indexed.IsValidResponse, indexed.DebugInformation);
            var found = await client.SearchAsync<JsonElement>(d => d.Indices(index).Query(q => q.Nested(n => n
                .Path("measurements").Query(nq => nq.Term(t => t.Field("measurements.value").Value(0))))), TestCancellationToken);
            Assert.True(found.IsValidResponse, found.DebugInformation);
            Assert.Equal("native", Assert.Single(found.Hits).Id);
        }
        finally
        {
            await client.Indices.DeleteAsync(index, TestCancellationToken);
        }
    }
}
