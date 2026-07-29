using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Xunit;

namespace Exceptionless.Tests;

public class AppHostConfigurationTests
{
    [Fact]
    public async Task CreateAsync_WithSeparateElasticsearchAndKibanaOverrides_UsesIndependentImageTags()
    {
        const string elasticsearchImageTag = "9.4.4-sha256-candidate";
        const string kibanaImageTag = "9.4.4";
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Exceptionless_AppHost>(
            [
                $"--Elasticsearch:ImageTag={elasticsearchImageTag}",
                $"--Elasticsearch:KibanaImageTag={kibanaImageTag}"
            ],
            TestContext.Current.CancellationToken);

        var elasticsearch = Assert.Single(appHost.Resources.OfType<ElasticsearchResource>());
        var kibana = Assert.Single(appHost.Resources.OfType<KibanaResource>());
        var elasticsearchImage = Assert.Single(elasticsearch.Annotations.OfType<ContainerImageAnnotation>());
        var kibanaImage = Assert.Single(kibana.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal(elasticsearchImageTag, elasticsearchImage.Tag);
        Assert.Equal(kibanaImageTag, kibanaImage.Tag);
    }
}
