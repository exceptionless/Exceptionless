using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
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

        protected override async Task SendQueryNotificationsAsync(ChangeType changeType, object query) {
            if (!NotificationsEnabled)
                return;

            var eq = query as ExceptionlessQuery;
            if (eq == null) {
                await base.SendQueryNotificationsAsync(changeType, query).AnyContext();
                return;
            }

            var delay = TimeSpan.FromSeconds(1.5);
            if (eq.Ids.Count > 0) {
                foreach (var id in eq.Ids) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        Id = id,
                        OrganizationId = eq.OrganizationIds.Count == 1 ? eq.OrganizationIds.Single() : null,
                        ProjectId = eq.ProjectIds.Count == 1 ? eq.ProjectIds.Single() : null,
                        StackId = eq.StackIds.Count == 1 ? eq.StackIds.Single() : null,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (eq.StackIds.Count > 0) {
                foreach (var stackId in eq.StackIds) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        OrganizationId = eq.OrganizationIds.Count == 1 ? eq.OrganizationIds.Single() : null,
                        ProjectId = eq.ProjectIds.Count == 1 ? eq.ProjectIds.Single() : null,
                        StackId = stackId,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (eq.ProjectIds.Count > 0) {
                foreach (var projectId in eq.ProjectIds) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = changeType,
                        OrganizationId = eq.OrganizationIds.Count == 1 ? eq.OrganizationIds.Single() : null,
                        ProjectId = projectId,
                        Type = EntityTypeName
                    }, delay).AnyContext();
                }

                return;
            }

            if (eq.OrganizationIds.Count > 0) {
                foreach (var organizationId in eq.OrganizationIds) {
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
