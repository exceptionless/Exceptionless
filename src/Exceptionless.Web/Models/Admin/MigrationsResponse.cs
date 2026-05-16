namespace Exceptionless.Web.Models.Admin;

public record MigrationsResponse(
    int CurrentVersion,
    Foundatio.Repositories.Migrations.MigrationState[]? States
);
