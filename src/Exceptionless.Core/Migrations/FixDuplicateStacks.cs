using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public sealed class FixDuplicateStacks : MigrationBase {
        private readonly IElasticClient _client;
        private readonly IStackRepository _stackRepository;
        private readonly ExceptionlessElasticConfiguration _config;

        public FixDuplicateStacks(ExceptionlessElasticConfiguration configuration, IStackRepository stackRepository, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
            _stackRepository = stackRepository;

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
            var lastStatus = DateTime.MinValue;

            _logger.LogInformation($"Found {total} duplicate stacks.");

            foreach (var duplicateSignature in buckets) {
                string projectId = null;
                string signature = null;
                try {
                    var parts = duplicateSignature.KeyAsString.Split(':');
                    if (parts.Length != 2) {
                        _logger.LogError("Error parsing duplicate signature {DuplicateSignature}", duplicateSignature.KeyAsString);
                        continue;
                    }
                    projectId = duplicateSignature.KeyAsString.Split(':').First();
                    signature = duplicateSignature.KeyAsString.Split(':').Last();

                    var stacks = await _stackRepository.FindAsync(q => q.Project(projectId).FilterExpression($"signature_hash:{signature}"));
                    var targetStack = stacks.Documents.OrderBy(s => s.CreatedUtc).First();
                    var duplicateStacks = stacks.Documents.OrderBy(s => s.CreatedUtc).Skip(1).ToList();

                    targetStack.Status = stacks.Documents.FirstOrDefault(d => d.Status != StackStatus.Open)?.Status ?? StackStatus.Open;
                    targetStack.LastOccurrence = stacks.Documents.Max(d => d.LastOccurrence);
                    targetStack.SnoozeUntilUtc = stacks.Documents.Max(d => d.SnoozeUntilUtc);
                    targetStack.DateFixed = stacks.Documents.Max(d => d.DateFixed); ;
                    targetStack.UpdatedUtc = stacks.Documents.Max(d => d.UpdatedUtc);
                    targetStack.TotalOccurrences += duplicateStacks.Sum(d => d.TotalOccurrences);
                    targetStack.Tags.AddRange(duplicateStacks.SelectMany(d => d.Tags));
                    targetStack.References = stacks.Documents.SelectMany(d => d.References).Distinct().ToList();
                    targetStack.OccurrencesAreCritical = stacks.Documents.Any(d => d.OccurrencesAreCritical);
                    
                    await _stackRepository.RemoveAsync(duplicateStacks);
                    await _stackRepository.SaveAsync(targetStack);
                    processed++;

                    if (DateTime.Now.Subtract(lastStatus) > TimeSpan.FromSeconds(5)) {
                        lastStatus = DateTime.Now;
                        _logger.LogInformation("Fixing duplicate stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                    }
                } catch (Exception ex) {
                    error++;
                    _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}", projectId, signature);
                }
            }
        }
    }
}