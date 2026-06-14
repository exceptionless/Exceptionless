using Foundatio.Mediator;

namespace Exceptionless.Web.Api.Infrastructure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class HiddenEndpointAttribute : Attribute, IEndpointConvention<RouteHandlerBuilder>
{
    public void Configure(RouteHandlerBuilder builder)
    {
        builder.ExcludeFromDescription();
    }
}
