using Foundatio.Storage;

namespace Aspire.Hosting;

public static class MinIoExtensions
{
    public static IResourceBuilder<MinIoResource> AddMinIo(
        this IDistributedApplicationBuilder builder,
        string name,
        Action<MinIoBuilder>? configure = null)
    {
        var options = new MinIoBuilder();
        configure?.Invoke(options);

        var resource = new MinIoResource(name, options.AccessKey, options.SecretKey, options.Bucket ?? "storage");

        string? connectionString = null;

        builder.Eventing.Subscribe<ResourceReadyEvent>(resource, async (@event, ct) =>
        {
            connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
                throw new DistributedApplicationException($"ResourceReadyEvent was published for the '{resource.Name}' resource but the connection string was null.");

            var storage = new S3FileStorage(o => o.ConnectionString(connectionString));
            try
            {
                storage.Client.PutBucketAsync(options.Bucket ?? "storage", ct).GetAwaiter().GetResult();
            }
            catch
            {
                // ignored
            }
        });

        return builder.AddResource(resource)
            .WithImage(MinIoContainerImageTags.Image)
            .WithImageRegistry(MinIoContainerImageTags.Registry)
            .WithImageTag(MinIoContainerImageTags.Tag)
            .WithArgs("server", "/data", "--console-address", $":{MinIoResource.DefaultConsolePort}")
            .WithEndpoint(port: options.ApiPort, targetPort: MinIoResource.DefaultApiPort, name: MinIoResource.ApiEndpointName)
            .WithHttpEndpoint(port: options.ConsolePort, targetPort: MinIoResource.DefaultConsolePort, name: MinIoResource.ConsoleEndpointName)
            .ConfigureCredentials(options)
            .ConfigureVolume(options);
    }

    private static IResourceBuilder<MinIoResource> ConfigureCredentials(
        this IResourceBuilder<MinIoResource> builder,
        MinIoBuilder options)
    {
        return builder
            .WithEnvironment("MINIO_ROOT_USER", options.AccessKey ?? "minioadmin")
            .WithEnvironment("MINIO_ROOT_PASSWORD", options.SecretKey ?? "minioadmin");
    }

    private static IResourceBuilder<MinIoResource> ConfigureVolume(
        this IResourceBuilder<MinIoResource> builder,
        MinIoBuilder options)
    {
        if (!string.IsNullOrEmpty(options.DataVolumePath))
            builder = builder.WithVolume(options.DataVolumePath, "/data");

        return builder;
    }
}

public class MinIoResource(string name, string? accessKey = null, string? secretKey = null, string? bucket = "storage")
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string ApiEndpointName = "api";
    internal const string ConsoleEndpointName = "console";
    internal const int DefaultApiPort = 9000;
    internal const int DefaultConsolePort = 9001;

    private EndpointReference? _apiReference;
    private EndpointReference? _consoleReference;

    private EndpointReference ApiEndpoint =>
        _apiReference ??= new EndpointReference(this, ApiEndpointName);

    private EndpointReference ConsoleEndpoint =>
        _consoleReference ??= new EndpointReference(this, ConsoleEndpointName);

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"ServiceUrl=http://{ApiEndpoint.Property(EndpointProperty.Host)}:{ApiEndpoint.Property(EndpointProperty.Port)};" +
            $"AccessKey={AccessKey ?? "minioadmin"};" +
            $"SecretKey={SecretKey ?? "minioadmin"};" +
            $"Bucket={Bucket}");

    public string? AccessKey { get; } = accessKey;
    public string? SecretKey { get; } = secretKey;
    public string? Bucket { get; } = bucket;
}

public class MinIoBuilder
{
    public int? ApiPort { get; set; }
    public int? ConsolePort { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Bucket { get; set; }
    public string? DataVolumePath { get; set; }

    public MinIoBuilder WithPorts(int? apiPort = null, int? consolePort = null)
    {
        ApiPort = apiPort;
        ConsolePort = consolePort;
        return this;
    }

    public MinIoBuilder WithCredentials(string accessKey, string secretKey)
    {
        AccessKey = accessKey;
        SecretKey = secretKey;
        return this;
    }

    public MinIoBuilder WithBucket(string bucket)
    {
        Bucket = bucket;
        return this;
    }

    public MinIoBuilder WithDataVolume(string path)
    {
        DataVolumePath = path;
        return this;
    }
}

internal static class MinIoContainerImageTags
{
    internal const string Registry = "docker.io";
    internal const string Image = "minio/minio";
    internal const string Tag = "latest";
}
