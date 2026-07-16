using Exceptionless.Core.Authorization;
using Exceptionless.Web.Api.Filters;
using Foundatio.Mediator;

[assembly: MediatorConfiguration(EndpointDiscovery = EndpointDiscovery.Explicit)]
[assembly: MediatorEndpointGroup(
    Name = "Admin",
    RoutePrefix = "/api/v2/admin",
    Policies = [AuthorizationRoles.GlobalAdminPolicy],
    EndpointFilters = [typeof(AutoValidationEndpointFilter)],
    ExcludeFromDescription = true)]
