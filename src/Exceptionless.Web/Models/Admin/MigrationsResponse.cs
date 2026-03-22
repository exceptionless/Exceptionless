namespace Exceptionless.Web.Models.Admin;

public record MigrationsResponse(
    int CurrentVersion = 0,
    Foundatio.Repositories.Migrations.MigrationState[]? States = null
);
