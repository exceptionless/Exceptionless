using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(ExceptionlessElasticConfiguration configuration, IEventRepository eventRepository, IValidator<Stack> validator)
            : base(configuration.Stacks.Stack, validator) {
            _eventRepository = eventRepository;
            DocumentsChanging.AddHandler(OnDocumentChangingAsync);
            FieldsRequiredForRemove.Add(GetPropertyName(nameof(Stack.SignatureHash)));
        }

        private async Task OnDocumentChangingAsync(object sender, DocumentsChangeEventArgs<Stack> args) {
            if (args.ChangeType != ChangeType.Removed)
                return;

            foreach (var document in args.Documents) {
                if (await _eventRepository.GetCountByStackIdAsync(document.Value.Id).AnyContext() > 0)
                    throw new ApplicationException($"Stack \"{document.Value.Id}\" can't be deleted because it has events associated to it.");
            }
        }

        protected override async Task AddToCacheAsync(ICollection<Stack> documents, TimeSpan? expiresIn = null) {
            if (!IsCacheEnabled)
                return;

            await base.AddToCacheAsync(documents, expiresIn).AnyContext();
            foreach (var stack in documents)
                await Cache.SetAsync(GetStackSignatureCacheKey(stack), stack, expiresIn ?? TimeSpan.FromSeconds(ElasticType.DefaultCacheExpirationSeconds)).AnyContext();
        }

        private string GetStackSignatureCacheKey(Stack stack) {
            return GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
        }

        private string GetStackSignatureCacheKey(string projectId, string signatureHash) {
            return String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
        }

        public async Task IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            // Only update the LastOccurrence if the new date is greater then the existing date.
            const string script = @"
Instant parseDate(def dt) {
  if (dt != null) {
    try {
      return Instant.parse(dt);
    } catch(DateTimeParseException e) {}
  }
  return Instant.MIN;
}

if (ctx._source.total_occurrences == 0 || parseDate(ctx._source.first_occurrence).isAfter(parseDate(params.minOccurrenceDateUtc))) {
  ctx._source.first_occurrence = params.minOccurrenceDateUtc;
}
if (parseDate(ctx._source.last_occurrence).isBefore(parseDate(params.maxOccurrenceDateUtc))) {
  ctx._source.last_occurrence = params.maxOccurrenceDateUtc;
}
ctx._source.total_occurrences += params.count;";

            var request = new UpdateRequest<Stack, Stack>(GetIndexById(stackId), ElasticType.Type, stackId) {
                RetryOnConflict = 3,
                Script = new InlineScript(script.Replace("\r\n", String.Empty).Replace("    ", " ")) {
                    Params = new Dictionary<string, object>(3) {
                        { "minOccurrenceDateUtc", minOccurrenceDateUtc },
                        { "maxOccurrenceDateUtc", maxOccurrenceDateUtc },
                        { "count", count }
                    }
                }
            };

            var result = await _client.UpdateAsync<Stack>(request).AnyContext();
            if (!result.IsValid) {
                _logger.Error("Error occurred incrementing total event occurrences on stack \"{0}\". Error: {1}", stackId, result.ServerError.Error);
                return;
            }

            if (IsCacheEnabled)
                await Cache.RemoveAsync(stackId).AnyContext();

            if (sendNotifications) {
                await PublishMessageAsync(new ExtendedEntityChanged {
                    ChangeType = ChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    Type = EntityTypeName
                }, TimeSpan.FromSeconds(1.5)).AnyContext();
            }
        }

        public async Task<Stack> GetStackBySignatureHashAsync(string projectId, string signatureHash) {
            var key = GetStackSignatureCacheKey(projectId, signatureHash);
            Stack stack = IsCacheEnabled ? await Cache.GetAsync(key, default(Stack)).AnyContext() : null;
            if (stack != null)
                return stack;

            var hit = await FindOneAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(Query<Stack>.Term(s => s.SignatureHash, signatureHash))).AnyContext();

            if (IsCacheEnabled && hit != null)
                await Cache.SetAsync(key, hit.Document, TimeSpan.FromSeconds(ElasticType.DefaultCacheExpirationSeconds)).AnyContext();

            return hit?.Document;
        }

        public Task<FindResults<Stack>> GetByFilterAsync(IExceptionlessSystemFilterQuery systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (String.IsNullOrEmpty(sort))
                sort = $"-{GetPropertyName(nameof(Stack.LastOccurrence))}";

            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? GetPropertyName(nameof(Stack.LastOccurrence)))
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(sort);

            return FindAsync(search);
        }

        public async Task MarkAsRegressedAsync(string stackId) {
            var stack = await GetByIdAsync(stackId).AnyContext();
            stack.IsRegressed = true;
            await SaveAsync(stack, true).AnyContext();
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents) {
            if (!IsCacheEnabled)
                return;

            var keys = documents.UnionOriginalAndModified().Select(GetStackSignatureCacheKey).Distinct();
            await Cache.RemoveAllAsync(keys).AnyContext();
            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}