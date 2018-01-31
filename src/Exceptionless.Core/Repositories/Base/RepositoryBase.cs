using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {
        public RepositoryBase(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            NotificationsEnabled = Settings.Current.EnableRepositoryNotifications;
        }

        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, T document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
            if (!NotificationsEnabled)
                return Task.CompletedTask;

            string organizationId = (document as IOwnedByOrganization)?.OrganizationId;
            string projectId = (document as IOwnedByProject)?.ProjectId;
            string stackId = (document as IOwnedByStack)?.StackId;
            return PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId, document?.Id, data), delay);
        }

        protected override Task SendQueryNotificationsAsync(ChangeType changeType, IRepositoryQuery query, ICommandOptions options) {
            if (!NotificationsEnabled || !options.ShouldNotify())
                return Task.CompletedTask;

            var delay = TimeSpan.FromSeconds(1.5);
            var organizations = query.GetOrganizations();
            var projects = query.GetProjects();
            var stacks = query.GetStacks();
            var ids = query.GetIds();
            var tasks = new List<Task>();

            string organizationId = organizations.Count == 1 ? organizations.Single() : null;
            if (ids.Count > 0) {
                string projectId = projects.Count == 1 ? projects.Single() : null;
                string stackId = stacks.Count == 1 ? stacks.Single() : null;

                foreach (string id in ids)
                    tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId, id), delay));

                return Task.WhenAll(tasks);
            }

            if (stacks.Count > 0) {
                string projectId = projects.Count == 1 ? projects.Single() : null;
                foreach (string stackId in stacks)
                    tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId, stackId), delay));

                return Task.WhenAll(tasks);
            }

            if (projects.Count > 0) {
                foreach (string projectId in projects)
                    tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organizationId, projectId), delay));

                return Task.WhenAll(tasks);
            }

            if (organizations.Count > 0) {
                foreach (string organization in organizations)
                    tasks.Add(PublishMessageAsync(CreateEntityChanged(changeType, organization), delay));

                return Task.WhenAll(tasks);
            }

            return PublishMessageAsync(new EntityChanged {
                ChangeType = changeType,
                Type = EntityTypeName
            }, delay);
        }

        protected EntityChanged CreateEntityChanged(ChangeType changeType, string organizationId = null, string projectId = null, string stackId = null, string id = null, IDictionary<string, object> data = null) {
            var model = new EntityChanged {
                ChangeType = changeType,
                Type = EntityTypeName,
                Id = id
            };

            if (data != null) {
                foreach (var kvp in data)
                    model.Data[kvp.Key] = kvp.Value;
            }

            if (organizationId != null)
                model.Data[ExtendedEntityChanged.KnownKeys.OrganizationId] = organizationId;

            if (projectId != null)
                model.Data[ExtendedEntityChanged.KnownKeys.ProjectId] = projectId;

            if (stackId != null)
                model.Data[ExtendedEntityChanged.KnownKeys.StackId] = stackId;

            return model;
        }
    }
}
