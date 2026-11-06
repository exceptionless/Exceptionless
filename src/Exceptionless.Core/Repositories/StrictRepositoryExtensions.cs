using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

internal static class StrictRepositoryExtensions
{
    public static Task<IReadOnlyCollection<T>> GetByIdsStrictAsync<T>(
        this IReadOnlyRepository<T> repository,
        Ids ids,
        CommandOptionsDescriptor<T>? options = null,
        CancellationToken cancellationToken = default)
        where T : class, IIdentity, new()
    {
        // The cleanup job's production repositories are all RepositoryBase implementations.
        // Unsupported substitutes must fail closed instead of treating unreadable parents as absent.
        if (repository is not RepositoryBase<T> exceptionlessRepository)
            throw new NotSupportedException($"Repository {repository.GetType().Name} does not support strict multi-get reads.");

        return exceptionlessRepository.GetByIdsStrictAsync(ids, options, cancellationToken);
    }
}
