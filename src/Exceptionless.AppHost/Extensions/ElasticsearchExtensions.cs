using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using HealthChecks.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Elasticsearch resources to the application model.
/// </summary>
public static class ElasticsearchBuilderExtensions
{
    private const int ElasticsearchPort = 9200;
    private const int ElasticsearchInternalPort = 9300;
    private const int KibanaPort = 5601;

    /// <summary>
    /// Adds a Elasticsearch container to the application model. The default image is "docker.elastic.co/elasticsearch/elasticsearch". This version the package defaults to the 8.17.0 tag of the Elasticsearch container image
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ElasticsearchResource> AddElasticsearch(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var elasticsearch = new ElasticsearchResource(name);

        string? connectionString = null;
        ElasticsearchOptions? options = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(elasticsearch, async (@event, ct) =>
        {
            connectionString = await elasticsearch.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{elasticsearch.Name}' resource but the connection string was null.");
            }

            options = new ElasticsearchOptions();
            options.UseServer(connectionString);
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
          .Add(new HealthCheckRegistration(
              healthCheckKey,
              sp => new ElasticsearchHealthCheck(options!),
              failureStatus: default,
              tags: default,
              timeout: default));

        return builder.AddResource(elasticsearch)
            .WithImage(ElasticsearchContainerImageTags.Image, ElasticsearchContainerImageTags.Tag)
            .WithImageRegistry(ElasticsearchContainerImageTags.ElasticsearchRegistry)
            .WithHttpEndpoint(targetPort: ElasticsearchPort, port: port, name: ElasticsearchResource.PrimaryEndpointName)
            .WithEndpoint(targetPort: ElasticsearchInternalPort, name: ElasticsearchResource.InternalEndpointName)
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("action.destructive_requires_name", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms1g -Xmx1g")
            .WithHealthCheck(healthCheckKey)
            .PublishAsConnectionString();
    }

    public static IResourceBuilder<ElasticsearchResource> WithKibana(this IResourceBuilder<ElasticsearchResource> builder, Action<IResourceBuilder<KibanaResource>>? configureContainer = null, string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.ApplicationBuilder.Resources.OfType<KibanaResource>().SingleOrDefault() is { } existingKibanaResource)
        {
            var builderForExistingResource = builder.ApplicationBuilder.CreateResourceBuilder(existingKibanaResource);
            configureContainer?.Invoke(builderForExistingResource);
            return builder;
        }
        else
        {
            containerName ??= $"{builder.Resource.Name}-kibana";

            builder.ApplicationBuilder.Services.TryAddLifecycleHook<KibanaConfigWriterHook>();

            var resource = new KibanaResource(containerName);
            var resourceBuilder = builder.ApplicationBuilder.AddResource(resource)
                                      .WithImage(ElasticsearchContainerImageTags.KibanaImage, ElasticsearchContainerImageTags.Tag)
                                      .WithImageRegistry(ElasticsearchContainerImageTags.KibanaRegistry)
                                      .WithHttpEndpoint(targetPort: KibanaPort, name: containerName)
                                      .WithEnvironment("xpack.security.enabled", "false")
                                      .ExcludeFromManifest();

            configureContainer?.Invoke(resourceBuilder);

            return builder;
        }
    }

    public static IResourceBuilder<ElasticsearchResource> WithDataVolume(this IResourceBuilder<ElasticsearchResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"), "/usr/share/elasticsearch/data");
    }

    public static IResourceBuilder<ElasticsearchResource> WithDataBindMount(this IResourceBuilder<ElasticsearchResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/usr/share/elasticsearch/data");
    }
}

internal static class ElasticsearchContainerImageTags
{
    public const string ElasticsearchRegistry = "docker.io";
    public const string Image = "exceptionless/elasticsearch";
    public const string KibanaRegistry = "docker.elastic.co";
    public const string KibanaImage = "kibana/kibana";
    public const string Tag = "8.17.0";
}
