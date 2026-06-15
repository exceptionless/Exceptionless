using Microsoft.AspNetCore.Http.Metadata;

namespace Exceptionless.Web.Api.Infrastructure;

public static class EndpointConventionExtensions
{
    public static RouteHandlerBuilder AcceptAnyJsonContentType(this RouteHandlerBuilder builder)
    {
        builder.Add(endpointBuilder =>
        {
            for (int index = endpointBuilder.Metadata.Count - 1; index >= 0; index--)
            {
                if (endpointBuilder.Metadata[index] is IAcceptsMetadata)
                    endpointBuilder.Metadata.RemoveAt(index);
            }
        });

        return builder;
    }
}
