using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;

namespace Exceptionless.Core.Repositories;

public sealed class SystemSettingsRepository : RepositoryBase<SystemSettings>, ISystemSettingsRepository
{
    public SystemSettingsRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.SystemSettings, validator, options)
    {
        DefaultConsistency = Consistency.Immediate;
    }
}
