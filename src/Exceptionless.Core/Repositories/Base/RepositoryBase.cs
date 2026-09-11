using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.MGet;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories;

public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new()
{
    protected readonly MiniValidationValidator _validator;
    protected readonly AppOptions _options;

    public RepositoryBase(IIndex index, MiniValidationValidator validator, AppOptions options) : base(index)
    {
        _validator = validator;
        _options = options;
        NotificationsEnabled = options.EnableRepositoryNotifications;
    }

    /// <summary>
    /// Gets documents in real time and rejects top-level or per-document multi-get errors.
    /// Use this when a missing result can trigger a destructive operation: Elasticsearch can
    /// return HTTP 200 with an error for an individual item, which the standard repository
    /// multi-get intentionally logs and omits from its result set. This bypasses cache reads so
    /// the result reflects Elasticsearch's real-time view.
    /// </summary>
    internal async Task<IReadOnlyCollection<T>> GetByIdsStrictAsync(
        Foundatio.Repositories.Ids ids,
        CommandOptionsDescriptor<T>? options = null,
        CancellationToken cancellationToken = default)
    {
        var idList = ids?.Distinct().Where(id => !String.IsNullOrEmpty(id)).ToList();
        if (idList is not { Count: > 0 })
            return [];
        if (HasParent || ElasticIndex.HasMultipleIndexes)
            throw new NotSupportedException("Strict multi-get only supports single-index repositories without parent routing.");

        var remainingIds = idList.Select(id => id.Value).ToHashSet(StringComparer.Ordinal);
        if (remainingIds.Count != idList.Count)
            throw new NotSupportedException("Strict multi-get does not support duplicate IDs with different routing values.");

        var configuredOptions = ConfigureOptions(options.Configure());
        await OnBeforeGetAsync(new Foundatio.Repositories.Ids(idList), configuredOptions, typeof(T));
        var operations = idList.Select(id =>
        {
            var operation = new MultiGetOperation(id.Value) { Index = ElasticIndex.GetIndex(id) };
            if (id.Routing is not null)
                operation.Routing = id.Routing;

            return operation;
        }).ToList();

        var request = new MultiGetRequestDescriptor().Docs(operations);
        ConfigureMultiGetRequest(request, configuredOptions);

        var response = await _client.MultiGetAsync<T>(request, cancellationToken);
        _logger.LogRequest(response, configuredOptions.GetQueryLogLevel());
        if (!response.IsValidResponse)
            throw new DocumentException($"Error getting documents: {response.DebugInformation}", response.ApiCallDetails.OriginalException);

        var documents = new List<T>();
        foreach (var item in response.Docs)
        {
            item.Match(
                result =>
                {
                    if (result is null || String.IsNullOrEmpty(result.Id) || !remainingIds.Remove(result.Id))
                        throw new DocumentException("Elasticsearch returned an invalid multi-get item.");

                    if (result.Found)
                    {
                        if (result.Source is null || !String.Equals(result.Source.Id, result.Id, StringComparison.Ordinal))
                            throw new DocumentException($"Elasticsearch returned document {result.Id} without a matching source.");

                        if (result.Source is IVersioned versionedDocument && result.PrimaryTerm.HasValue && result.SeqNo.HasValue)
                            versionedDocument.Version = new ElasticDocumentVersion(result.PrimaryTerm.Value, result.SeqNo.Value);

                        if (ShouldReturnDocument(result.Source, configuredOptions))
                            documents.Add(result.Source);
                    }
                    else if (result.Source is not null)
                    {
                        throw new DocumentException($"Elasticsearch returned a source for missing document {result.Id}.");
                    }
                },
                error =>
                {
                    if (error is null)
                        throw new DocumentException("Elasticsearch returned an invalid multi-get error item.");

                    throw new DocumentException($"Error getting document {error.Id} from index {error.Index}: {error.Error?.Reason}");
                });
        }

        if (remainingIds.Count > 0)
            throw new DocumentException($"Elasticsearch omitted {remainingIds.Count} requested multi-get item(s).");

        return documents.AsReadOnly();
    }

    protected override Task ValidateAndThrowAsync(T document)
    {
        return _validator.ValidateAndThrowAsync(document);
    }

    protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, T? document, IDictionary<string, object?>? data = null, TimeSpan? delay = null)
    {
        if (!NotificationsEnabled)
            return Task.CompletedTask;

        string? organizationId = (document as IOwnedByOrganization)?.OrganizationId;
        string? projectId = (document as IOwnedByProject)?.ProjectId;
        string? stackId = (document as IOwnedByStack)?.StackId;
        return PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId, document?.Id, data), delay);
    }

    protected override Task SendQueryNotificationsAsync(ChangeType changeType, IRepositoryQuery query, ICommandOptions options)
    {
        if (!NotificationsEnabled || !options.ShouldNotify())
            return Task.CompletedTask;

        var delay = TimeSpan.FromSeconds(1.5);
        var organizations = query.GetOrganizations();
        var projects = query.GetProjects();
        var stacks = query.GetStacks();
        var ids = query.GetIds();
        var tasks = new List<Task>();

        string? organizationId = organizations.Count == 1 ? organizations.Single() : null;
        if (ids.Count > 0)
        {
            string? projectId = projects.Count == 1 ? projects.Single() : null;
            string? stackId = stacks.Count == 1 ? stacks.Single() : null;

            foreach (string id in ids)
                tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId, id), delay));

            return Task.WhenAll(tasks);
        }

        if (stacks.Count > 0)
        {
            string? projectId = projects.Count == 1 ? projects.Single() : null;
            foreach (string stackId in stacks)
                tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId), delay));

            return Task.WhenAll(tasks);
        }

        if (projects.Count > 0)
        {
            foreach (string projectId in projects)
                tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId), delay));

            return Task.WhenAll(tasks);
        }

        if (organizations.Count > 0)
        {
            foreach (string organization in organizations)
                tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organization), delay));

            return Task.WhenAll(tasks);
        }

        return PublishMessageAsync(new EntityChanged
        {
            ChangeType = changeType,
            Type = EntityTypeName
        }, delay);
    }

    protected EntityChanged CreateEntityChanged(ChangeType changeType, string? organizationId = null, string? projectId = null, string? stackId = null, string? id = null, IDictionary<string, object?>? data = null)
    {
        var model = new EntityChanged
        {
            ChangeType = changeType,
            Type = EntityTypeName,
            Id = id
        };

        if (data is not null)
        {
            foreach (var kvp in data)
                model.Data[kvp.Key] = kvp.Value;
        }

        if (organizationId is not null)
            model.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;

        if (projectId is not null)
            model.Data[ExtendedEntityChanged.KnownKeys.ProjectId] = projectId;

        if (stackId is not null)
            model.Data[ExtendedEntityChanged.KnownKeys.StackId] = stackId;

        return model;
    }
}
