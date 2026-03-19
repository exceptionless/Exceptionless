using Foundatio.Repositories.Models;

namespace Exceptionless.Web.Models.Admin;

public record AdminStatsResponse(
    CountResult Organizations,
    CountResult Users,
    CountResult Projects,
    CountResult Stacks,
    CountResult Events
);
