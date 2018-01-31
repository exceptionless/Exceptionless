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
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(ExceptionlessElasticConfiguration configuration, IEventRepository eventRepository, IValidator<Stack> validator)
            : base(configuration.Stacks.Stack, validator) {
            _eventRepository = eventRepository;
            DocumentsChanging.AddHandler(OnDocumentChangingAsync);
            FieldsRequiredForRemove.Add(ElasticType.GetFieldName(s => s.SignatureHash));
        }

        private async Task OnDocumentChangingAsync(object sender, DocumentsChangeEventArgs<Stack> args) {
            if (args.ChangeType != ChangeType.Removed)
                return;

            foreach (var document in args.Documents) {
                if (await _eventRepository.GetCountByStackIdAsync(document.Value.Id).AnyContext() > 0)
                    throw new ApplicationException($"Stack \"{document.Value.Id}\" can't be deleted because it has events associated to it.");
            }
        }

        protected override async Task AddToCacheAsync(ICollection<Stack> documents, ICommandOptions options) {
            if (!IsCacheEnabled || Cache == null || !options.ShouldUseCache())
                return;

            await base.AddToCacheAsync(documents, options).AnyContext();
            foreach (var stack in documents)
                await Cache.SetAsync(GetStackSignatureCacheKey(stack), stack, options.GetExpiresIn()).AnyContext();
        }

        private string GetStackSignatureCacheKey(Stack stack) {
            return GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
        }

        private string GetStackSignatureCacheKey(string projectId, string signatureHash) {
            return String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
        }

        public async Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true) {
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
                _logger.LogError(result.OriginalException, "Error occurred incrementing total event occurrences on stack {stack}. Error: {Message}", stackId, result.ServerError?.Error);
                return false;
            }

            if (IsCacheEnabled)
                await Cache.RemoveAsync(stackId).AnyContext();

            if (sendNotifications)
                await PublishMessageAsync(CreateEntityChanged(ChangeType.Saved, organizationId, projectId, null, stackId), TimeSpan.FromSeconds(1.5)).AnyContext();

            return true;
        }

        public async Task<Stack> GetStackBySignatureHashAsync(string projectId, string signatureHash) {
            string key = GetStackSignatureCacheKey(projectId, signatureHash);
            var stack = IsCacheEnabled ? await Cache.GetAsync(key, default(Stack)).AnyContext() : null;
            if (stack != null)
                return stack;

            var hit = await FindOneAsync(q => q.Project(projectId).ElasticFilter(Query<Stack>.Term(s => s.SignatureHash, signatureHash))).AnyContext();
            if (IsCacheEnabled && hit != null)
                await Cache.SetAsync(key, hit.Document, TimeSpan.FromSeconds(((StackIndexType)ElasticType).DefaultCacheExpirationSeconds)).AnyContext();

            return hit?.Document;
        }

        public Task<FindResults<Stack>> GetByFilterAsync(ExceptionlessSystemFilter systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, CommandOptionsDescriptor<Stack> options = null) {
            IRepositoryQuery<Stack> query = new RepositoryQuery<Stack>()
                .DateRange(utcStart, utcEnd, field ?? ElasticType.GetFieldName(s => s.LastOccurrence))
                .SystemFilter(systemFilter)
                .FilterExpression(userFilter);

            query = !String.IsNullOrEmpty(sort) ? query.SortExpression(sort) : query.SortDescending(s => s.LastOccurrence);
            return FindAsync(q => query, options);
        }

        public async Task MarkAsRegressedAsync(string stackId) {
            var stack = await GetByIdAsync(stackId).AnyContext();
            stack.IsRegressed = true;
            await this.SaveAsync(stack, o => o.Cache()).AnyContext();
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents, ICommandOptions options = null) {
            if (!IsCacheEnabled)
                return;

            var keys = documents.UnionOriginalAndModified().Select(GetStackSignatureCacheKey).Distinct();
            await Cache.RemoveAllAsync(keys).AnyContext();
            await base.InvalidateCacheAsync(documents, options).AnyContext();
        }
    }
}
