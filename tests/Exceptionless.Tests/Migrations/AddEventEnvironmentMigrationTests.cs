using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class AddEventEnvironmentMigrationTests : IntegrationTestsBase
{
    public AddEventEnvironmentMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    [Fact]
    public async Task RunAsync_LegacyDailyIndex_AddsMappingAndCanRunAgain()
    {
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        string indexName = $"{configuration.Events.Name}-v0-2000.01.01";
        var client = configuration.Client;
        try
        {
            var created = await client.Indices.CreateAsync(indexName, index => index
                .Settings(settings => settings.Analysis(analysis => analysis.Analyzers(analyzers => analyzers
                    .Custom("lowerkeyword", analyzer => analyzer.Filter("lowercase").Tokenizer("keyword")))))
                .Mappings(mapping => mapping.Properties(properties => properties.Keyword("legacy_field"))), TestCancellationToken);
            Assert.True(created.IsValidResponse, created.DebugInformation);

            var migration = new AddEventEnvironment(configuration, GetService<ILoggerFactory>());
            var context = new MigrationContext(EmptyLock.Empty, _logger, TestCancellationToken);
            await migration.RunAsync(context);
            await migration.RunAsync(context);

            var mapping = await client.Indices.GetMappingAsync(indexName, TestCancellationToken);
            Assert.True(mapping.IsValidResponse, mapping.DebugInformation);
            var properties = Assert.Single(mapping.Mappings).Value.Mappings.Properties!;
            var environment = Assert.IsType<TextProperty>(properties["environment"]);
            Assert.Equal("lowerkeyword", environment.Analyzer);
            Assert.IsType<KeywordProperty>(environment.Fields!["keyword"]);
            Assert.IsType<KeywordProperty>(properties["legacy_field"]);
        }
        finally
        {
            await client.Indices.DeleteAsync(indexName, TestCancellationToken);
        }
    }
}
