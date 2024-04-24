﻿using System.Text;
using Aspire.Hosting.Lifecycle;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Elasticsearch resources to the application model.
/// </summary>
public static class ElasticsearchBuilderExtensions
{
    /// <summary>
    /// Adds a Elasticsearch container to the application model. The default image is "docker.elastic.co/elasticsearch/elasticsearch" and tag is "latest". This version the package defaults to the 8.12.2 tag of the Elasticsearch container image
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ElasticsearchResource> AddElasticsearch(this IDistributedApplicationBuilder builder, string name, int? port = null)
    {
        var elasticsearch = new ElasticsearchResource(name);
        return builder.AddResource(elasticsearch)
            .WithHttpEndpoint(port: port, targetPort: 9200, name: ElasticsearchResource.PrimaryEndpointName)
            .WithImage("elasticsearch/elasticsearch", "8.12.2")
            .WithImageRegistry("docker.elastic.co")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("xpack.security.enabled", "false")
            .WithEnvironment("action.destructive_requires_name", "false")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms1g -Xmx1g")
            .PublishAsConnectionString();
    }

    /// <summary>
    /// TODO: Doc Comments
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="containerName"></param>
    /// <param name="hostPort"></param>
    /// <returns></returns>
    public static IResourceBuilder<ElasticsearchResource> WithKibana(this IResourceBuilder<ElasticsearchResource> builder, string? containerName = null, int? hostPort = null)
    {
        if (builder.ApplicationBuilder.Resources.OfType<KibanaResource>().Any())
        {
            return builder;
        }

        builder.ApplicationBuilder.Services.TryAddLifecycleHook<KibanaConfigWriterHook>();

        containerName ??= $"{builder.Resource.Name}-kibana";

        var resource = new KibanaResource(containerName);
        builder.ApplicationBuilder.AddResource(resource)
                                  .WithAnnotation(new ContainerImageAnnotation { Image = "docker.elastic.co/kibana/kibana", Tag = "8.12.2" })
                                  .WithEnvironment("XPACK_SECURITY_ENABLED", "false")
                                  .WithHttpEndpoint(targetPort: 5601, port: hostPort, name: containerName)
                                  .ExcludeFromManifest();

        return builder;
    }
}

/// <summary>
/// A resource that represents a Elasticsearch resource independent of the hosting model.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class ElasticsearchResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Elasticsearch server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create($"http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the connection string expression for the Elasticsearch server for the manifest.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
            {
                return connectionStringAnnotation.Resource.ConnectionStringExpression;
            }

            return ConnectionString;
        }
    }

    /// <summary>
    /// Gets the connection string for the Elasticsearch server.
    /// </summary>
    /// <param name="cancellationToken"> A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the Elasticsearch server in the form "http://host:port".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionString.GetValueAsync(cancellationToken);
    }
}

/// <summary>
/// A resource that represents a Kibana container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class KibanaResource(string name) : ContainerResource(name)
{
}

internal class KibanaConfigWriterHook : IDistributedApplicationLifecycleHook
{
    public Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        if (appModel.Resources.OfType<KibanaResource>().SingleOrDefault() is not { } kibanaResource)
        {
            // No-op if there is no kibana resource (removed after hook added).
            return Task.CompletedTask;
        }

        var elasticsearchInstances = appModel.Resources.OfType<ElasticsearchResource>();

        if (!elasticsearchInstances.Any())
        {
            // No-op if there are no Elasticsearch resources present.
            return Task.CompletedTask;
        }

        var hostsVariableBuilder = new StringBuilder();

        foreach (var elasticsearchInstance in elasticsearchInstances)
        {
            if (elasticsearchInstance.PrimaryEndpoint.IsAllocated)
            {
                var hostString = $"{(hostsVariableBuilder.Length > 0 ? "," : string.Empty)}http://{elasticsearchInstance.PrimaryEndpoint.ContainerHost}:{elasticsearchInstance.PrimaryEndpoint.Port}";
                hostsVariableBuilder.Append(hostString);
            }
        }

        kibanaResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables.Add("ELASTICSEARCH_HOSTS", hostsVariableBuilder.ToString());
        }));

        return Task.CompletedTask;
    }
}
