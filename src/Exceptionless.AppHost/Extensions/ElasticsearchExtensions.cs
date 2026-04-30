using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Elasticsearch resources to the application model.
/// </summary>
public static class ElasticsearchBuilderExtensions
{
    private const int ElasticsearchPort = 9200;
    private const int KibanaPort = 5601;

    /// <summary>
    /// Adds a Elasticsearch container to the application model. The default image is "docker.elastic.co/elasticsearch/elasticsearch". This version the package defaults to the 8.19.14 tag of the Elasticsearch container image
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

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(elasticsearch, async (@event, ct) =>
        {
            connectionString = await elasticsearch.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{elasticsearch.Name}' resource but the connection string was null.");
            }
        });

        string healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
          .Add(new HealthCheckRegistration(
              healthCheckKey,
              sp => new ElasticsearchConnectionHealthCheck(() => connectionString),
              failureStatus: null,
              tags: null,
              timeout: null));

        return builder.AddResource(elasticsearch)
            .WithImage(ElasticsearchContainerImageTags.Image, ElasticsearchContainerImageTags.Tag)
            .WithImageRegistry(ElasticsearchContainerImageTags.ElasticsearchRegistry)
            .WithHttpEndpoint(targetPort: ElasticsearchPort, port: port, name: ElasticsearchResource.PrimaryEndpointName)
            .WithUrlForEndpoint(ElasticsearchResource.PrimaryEndpointName, u => u.DisplayText = "Elasticsearch")
            //.WithEndpoint(targetPort: ElasticsearchInternalPort, name: ElasticsearchResource.InternalEndpointName)
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

            var elasticsearch = builder.Resource;
            var resource = new KibanaResource(containerName);
            var resourceBuilder = builder.ApplicationBuilder.AddResource(resource)
                                      .WithImage(ElasticsearchContainerImageTags.KibanaImage, ElasticsearchContainerImageTags.Tag)
                                      .WithImageRegistry(ElasticsearchContainerImageTags.KibanaRegistry)
                                      .WithHttpEndpoint(targetPort: KibanaPort, name: containerName)
                                      .WithUrlForEndpoint(containerName, u => u.DisplayText = "Kibana")
                                      .WithEnvironment("xpack.security.enabled", "false")
                                      .WithEnvironment(ctx =>
                                      {
                                          // Use ReferenceExpression for proper container-to-container networking
                                          // The Host property will resolve to the container name when used this way
                                          var endpoint = elasticsearch.PrimaryEndpoint;
                                          var hostsExpr = ReferenceExpression.Create($"http://{endpoint.Property(EndpointProperty.Host)}:{endpoint.Property(EndpointProperty.Port)}");
                                          ctx.EnvironmentVariables["ELASTICSEARCH_HOSTS"] = hostsExpr;
                                      })
                                      .WaitFor(builder)
                                      .ExcludeFromManifest();

            configureContainer?.Invoke(resourceBuilder);

            return builder;
        }
    }

    public static IResourceBuilder<ElasticsearchResource> WithDataVolume(this IResourceBuilder<ElasticsearchResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/usr/share/elasticsearch/data");
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
    public const string Tag = "8.19.14";
}

internal sealed class ElasticsearchConnectionHealthCheck(Func<string?> connectionStringFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = connectionStringFactory();
        if (string.IsNullOrEmpty(connectionString))
            return new HealthCheckResult(context.Registration.FailureStatus, "Connection string not available.");

        var client = new ElasticsearchClient(new Uri(connectionString));
        var response = await client.PingAsync(cancellationToken);
        return response.IsValidResponse
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, $"Elasticsearch ping failed: {response.DebugInformation}");
    }
}
