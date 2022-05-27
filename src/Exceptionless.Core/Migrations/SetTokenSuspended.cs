using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class SetTokenSuspended : MigrationBase {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenRepository _tokenRepository;

    public SetTokenSuspended(IOrganizationRepository organizationRepository, ITokenRepository tokenRepository, ILoggerFactory loggerFactory) : base(loggerFactory) {
        _organizationRepository = organizationRepository;
        _tokenRepository = tokenRepository;

        MigrationType = MigrationType.Repeatable;
    }

    public override async Task RunAsync(MigrationContext context) {
        var suspendedOrgs = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.IsSuspended, true), o => o.SearchAfterPaging());
        _logger.LogInformation("Found {SuspendedOrgCount}", suspendedOrgs.Total);

        do {
            foreach (var suspendedOrg in suspendedOrgs.Documents) {
                await _tokenRepository.PatchAllAsync(q => q.Organization(suspendedOrg.Id).FieldEquals(t => t.IsSuspended, false), new PartialPatch(new { is_suspended = true }));
            }
        } while (await suspendedOrgs.NextPageAsync());
    }
}
