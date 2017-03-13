using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Queries;
using DataDictionary = Foundatio.Utility.DataDictionary;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new() {
        public RepositoryBase(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {}

        protected override Task PublishChangeTypeMessageAsync(ChangeType changeType, T document, IDictionary<string, object> data = null, TimeSpan? delay = null) {
          return PublishMessageAsync(new ExtendedEntityChanged {
                ChangeType = changeType,
                Id = document?.Id,
                OrganizationId = (document as IOwnedByOrganization)?.OrganizationId,
                ProjectId = (document as IOwnedByProject)?.ProjectId,
                StackId = (document as IOwnedByStack)?.StackId,
                Type = EntityTypeName,
                Data = new DataDictionary(data ?? new Dictionary<string, object>())
            }, delay);
        }

        protected override async Task SendQueryNotificationsAsync(ChangeType changeType, IRepositoryQuery query, ICommandOptions options) {
            if (!NotificationsEnabled || !options.ShouldNotify())
                return;

            var delay = TimeSpan.FromSeconds(1.5);
            var organizations = query.GetOrganizations();
            var projects = query.GetProjects();
            var stacks = query.GetStacks();
            var ids = query.GetIds();

            if (ids.Count > 0) {
                foreach (string id in ids) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        Id = id,
                        OrganizationId = organizations.Count == 1 ? organizations.Single() : null,
                        ProjectId = projects.Count == 1 ? projects.Single() : null,
                        StackId = stacks.Count == 1 ? stacks.Single() : null,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (stacks.Count > 0) {
                foreach (string stackId in stacks) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        OrganizationId = organizations.Count == 1 ? organizations.Single() : null,
                        ProjectId = projects.Count == 1 ? projects.Single() : null,
                        StackId = stackId,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (projects.Count > 0) {
                foreach (string projectId in projects) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        OrganizationId = organizations.Count == 1 ? organizations.Single() : null,
                        ProjectId = projectId,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (organizations.Count > 0) {
                foreach (string organizationId in organizations) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        OrganizationId = organizationId,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            await PublishMessageAsync(new EntityChanged {
                ChangeType = changeType,
                Type = EntityTypeName
            }, delay).AnyContext();
        }
    }
}
