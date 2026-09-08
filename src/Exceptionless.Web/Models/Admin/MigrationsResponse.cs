using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models.Admin;

public record MigrationsResponse(
    int CurrentVersion,
    MigrationStateResponse[] States
);

public record MigrationStateResponse(
    string Id,
    Foundatio.Repositories.Migrations.MigrationType MigrationType,
    int Version,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    string? ErrorMessage,
    bool CanRerun
);

public record RerunMigrationRequest([property: Required] string? Confirmation);

public record MigrationRerunOperationResponse(
    string Id,
    string MigrationId,
    int Version,
    string Source,
    string Status,
    string? RequestedByUserId,
    DateTime RequestedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    int AttemptCount,
    string? ErrorMessage
);
