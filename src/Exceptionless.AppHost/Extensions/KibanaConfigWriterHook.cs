using System.Text;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace Aspire.Hosting;

internal class KibanaConfigWriterHook : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var elasticsearchResources = new List<ElasticsearchResource>();

        eventing.Subscribe<ResourceEndpointsAllocatedEvent>((evt, _) =>
        {
            switch (evt.Resource)
            {
                case ElasticsearchResource elastic:
                    elasticsearchResources.Add(elastic);
                    break;
            }

            return Task.CompletedTask;
        });

        eventing.Subscribe<BeforeResourceStartedEvent>((evt, _) =>
        {
            if (evt.Resource is not KibanaResource kibanaResource)
                return Task.CompletedTask;

            if (elasticsearchResources.Count is 0)
                return Task.CompletedTask;

            var sb = new StringBuilder();
            foreach (var resource in elasticsearchResources.Where(elasticsearchInstance => elasticsearchInstance.PrimaryEndpoint.IsAllocated))
            {
                if (sb.Length > 0)
                    sb.Append(',');

                sb.Append($"{resource.PrimaryEndpoint.Scheme}://{resource.PrimaryEndpoint.Host}:{resource.PrimaryEndpoint.Port}");
            }

            kibanaResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                context.EnvironmentVariables.Add("ELASTICSEARCH_HOSTS", sb.ToString());
            }));

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }
}
