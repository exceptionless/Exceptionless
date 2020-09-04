using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public sealed class FixDuplicateStacks : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ICacheClient _cache;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ExceptionlessElasticConfiguration _config;

        public FixDuplicateStacks(ExceptionlessElasticConfiguration configuration, IStackRepository stackRepository, IEventRepository eventRepository, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
            _cache = configuration.Cache;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;

            MigrationType = MigrationType.Repeatable;
        }

        public override async Task RunAsync(MigrationContext context) {
            _logger.LogInformation("Getting duplicate stacks");

            var duplicateStackAgg = await _client.SearchAsync<Stack>(q => q
                .QueryOnQueryString("is_deleted:false")
                .Size(0)
                .Aggregations(a => a.Terms("stacks", t => t.Field(f => f.DuplicateSignature).MinimumDocumentCount(2).Size(10000))));
            
            var buckets = duplicateStackAgg.Aggregations.Terms("stacks").Buckets;
            int total = buckets.Count;
            int processed = 0;
            int error = 0;
            var lastStatus = SystemClock.Now;
            int batch = 1;

            while (buckets.Count > 0) {
                _logger.LogInformation($"Found {total} duplicate stacks in batch #{batch}.");

                foreach (var duplicateSignature in buckets) {
                    string projectId = null;
                    string signature = null;
                    try {
                        var parts = duplicateSignature.Key.Split(':');
                        if (parts.Length != 2) {
                            _logger.LogError("Error parsing duplicate signature {DuplicateSignature}", duplicateSignature.KeyAsString);
                            continue;
                        }
                        projectId = parts[0];
                        signature = parts[1];

                        var stacks = await _stackRepository.FindAsync(q => q.Project(projectId).FilterExpression($"signature_hash:{signature}"));
                        var targetStack = stacks.Documents.OrderBy(s => s.CreatedUtc).First();
                        var duplicateStacks = stacks.Documents.OrderBy(s => s.CreatedUtc).Skip(1).ToList();

                        targetStack.Status = stacks.Documents.FirstOrDefault(d => d.Status != StackStatus.Open)?.Status ?? StackStatus.Open;
                        targetStack.LastOccurrence = stacks.Documents.Max(d => d.LastOccurrence);
                        targetStack.SnoozeUntilUtc = stacks.Documents.Max(d => d.SnoozeUntilUtc);
                        targetStack.DateFixed = stacks.Documents.Max(d => d.DateFixed); ;
                        targetStack.TotalOccurrences += duplicateStacks.Sum(d => d.TotalOccurrences);
                        targetStack.Tags.AddRange(duplicateStacks.SelectMany(d => d.Tags));
                        targetStack.References = stacks.Documents.SelectMany(d => d.References).Distinct().ToList();
                        targetStack.OccurrencesAreCritical = stacks.Documents.Any(d => d.OccurrencesAreCritical);

                        duplicateStacks.ForEach(s => s.IsDeleted = true);
                        await _stackRepository.SaveAsync(duplicateStacks);
                        await _stackRepository.SaveAsync(targetStack);
                        await _eventRepository.PatchAllAsync(q => q.Stack(duplicateStacks.Select(s => s.Id)), new PartialPatch(new { stack_id = targetStack.Id }));
                        processed++;

                        if (SystemClock.UtcNow.Subtract(lastStatus) > TimeSpan.FromSeconds(5)) {
                            lastStatus = SystemClock.UtcNow;
                            _logger.LogInformation("Fixing duplicate stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                            await _cache.RemoveByPrefixAsync(nameof(Stack));
                        }
                    }
                    catch (Exception ex) {
                        error++;
                        _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}", projectId, signature);
                    }
                }

                duplicateStackAgg = await _client.SearchAsync<Stack>(q => q
                    .QueryOnQueryString("is_deleted:false")
                    .Size(0)
                    .Aggregations(a => a.Terms("stacks", t => t.Field(f => f.DuplicateSignature).MinimumDocumentCount(2).Size(10000))));

                buckets = duplicateStackAgg.Aggregations.Terms("stacks").Buckets;
                total += buckets.Count;
                batch++;

                _logger.LogInformation("Invalidating stack cache");
                await _cache.RemoveByPrefixAsync(nameof(Stack));
            }
        }
    }
}