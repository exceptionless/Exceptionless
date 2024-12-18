using System.Text;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

internal class KibanaConfigWriterHook : IDistributedApplicationLifecycleHook
{
    public async Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        if (appModel.Resources.OfType<KibanaResource>().SingleOrDefault() is not { } kibanaResource)
            return;

        var elasticsearchInstances = appModel.Resources.OfType<ElasticsearchResource>();

        if (!elasticsearchInstances.Any())
            return;

        var hostsVariableBuilder = new StringBuilder();

        foreach (var elasticsearchInstance in elasticsearchInstances)
        {
            if (elasticsearchInstance.PrimaryEndpoint.IsAllocated)
            {
                var connectionString = await elasticsearchInstance.GetConnectionStringAsync();
                if (hostsVariableBuilder.Length > 0)
                    hostsVariableBuilder.Append(",");
                hostsVariableBuilder.Append(elasticsearchInstance.PrimaryEndpoint.Scheme).Append("://").Append(elasticsearchInstance.PrimaryEndpoint.ContainerHost).Append(":").Append(elasticsearchInstance.PrimaryEndpoint.Port);
            }
        }

        kibanaResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables.Add("ELASTICSEARCH_HOSTS", hostsVariableBuilder.ToString());
        }));
    }
}
