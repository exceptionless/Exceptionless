namespace Aspire.Hosting;

/// <summary>
/// A resource that represents a Elasticsearch resource independent of the hosting model.
/// </summary>
public class ElasticsearchResource : ContainerResource, IResourceWithConnectionString
{
    // this endpoint is used for all API calls over HTTP.
    // This includes search and aggregations, monitoring and anything else that uses a HTTP request.
    // All client libraries will use this port to talk to Elasticsearch
    internal const string PrimaryEndpointName = "http";

    //this endpoint is a custom binary protocol used for communications between nodes in a cluster.
    //For things like cluster updates, master elections, nodes joining/leaving, shard allocation
    internal const string InternalEndpointName = "internal";

    /// <param name="name">The name of the resource.</param>
    public ElasticsearchResource(string name) : base(name)
    {
    }

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? _internalEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Elasticsearch. This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the internal endpoint for the Elasticsearch. This endpoint used for communications between nodes in a cluster
    /// </summary>
    public EndpointReference InternalEndpoint => _internalEndpoint ??= new(this, InternalEndpointName);

    /// <summary>
    /// Gets the connection string expression for the Elasticsearch
    /// </summary>
    public ReferenceExpression ConnectionString =>
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
