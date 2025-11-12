using System.Text;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

internal static class KibanaConfigWriterExtensions
{
    public static IResourceBuilder<KibanaResource> ConfigureElasticsearchHosts(
        this IResourceBuilder<KibanaResource> builder,
        IEnumerable<ElasticsearchResource> elasticsearchResources)
    {
        builder.WithAnnotation(new EnvironmentCallbackAnnotation(async context =>
        {
            var hostsVariableBuilder = new StringBuilder();

            foreach (var elasticsearchInstance in elasticsearchResources)
            {
                if (elasticsearchInstance.PrimaryEndpoint.IsAllocated)
                {
                    if (hostsVariableBuilder.Length > 0)
                        hostsVariableBuilder.Append(",");
                    
                    var endpoint = elasticsearchInstance.PrimaryEndpoint;
                    hostsVariableBuilder.Append(endpoint.Scheme)
                        .Append("://")
                        .Append(endpoint.Host)
                        .Append(":")
                        .Append(endpoint.Port);
                }
            }

            if (hostsVariableBuilder.Length > 0)
            {
                context.EnvironmentVariables["ELASTICSEARCH_HOSTS"] = hostsVariableBuilder.ToString();
            }

            await Task.CompletedTask;
        }));

        return builder;
    }
}
