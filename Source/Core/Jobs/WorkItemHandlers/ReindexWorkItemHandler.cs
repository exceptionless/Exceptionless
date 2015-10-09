using System;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Foundatio.Logging;
using Nest;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ReindexWorkItemHandler : WorkItemHandlerBase {
        private readonly IElasticClient _client;

        public ReindexWorkItemHandler(IElasticClient client) {
            _client = client;
        }
        
        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<ReindexWorkItem>();

            Logger.Info().Message("Received reindex work item for new index {0}", workItem.NewIndex).Write();
            var startTime = DateTime.UtcNow.AddSeconds(-1);
            await context.ReportProgressAsync(0, "Starting reindex...").AnyContext();
            var result = await ReindexAsync(workItem, context, 0, 90, workItem.StartUtc).AnyContext();
            await context.ReportProgressAsync(90, $"Total: {result.Total} Completed: {result.Completed}").AnyContext();

            // TODO: Check to make sure the docs have been added to the new index before changing alias

            if (!String.IsNullOrEmpty(workItem.Alias)) {
                await _client.AliasAsync(x => x.Remove(a => a.Alias(workItem.Alias).Index(workItem.OldIndex)).Add(a => a.Alias(workItem.Alias).Index(workItem.NewIndex))).AnyContext();
                await context.ReportProgressAsync(98, $"Updated alias: {workItem.Alias} Remove: {workItem.OldIndex} Add: {workItem.NewIndex}").AnyContext();
            }

            await _client.RefreshAsync().AnyContext();
            var secondPassResult = await ReindexAsync(workItem, context, 90, 98, startTime).AnyContext();
            await context.ReportProgressAsync(98, $"Total: {secondPassResult.Total} Completed: {secondPassResult.Completed}").AnyContext();

            if (workItem.DeleteOld) {
                await _client.RefreshAsync().AnyContext();
                long newDocCount = (await _client.CountAsync(d => d.Index(workItem.OldIndex)).AnyContext()).Count;
                long oldDocCount = (await _client.CountAsync(d => d.Index(workItem.OldIndex)).AnyContext()).Count;
                await context.ReportProgressAsync(98, $"Old Docs: {oldDocCount} New Docs: {newDocCount}").AnyContext();
                if (newDocCount >= oldDocCount)
                    await _client.DeleteIndexAsync(d => d.Index(workItem.OldIndex)).AnyContext();
                await context.ReportProgressAsync(98, $"Deleted index: {workItem.OldIndex}").AnyContext();
            }
            await context.ReportProgressAsync(100).AnyContext();
        }

        private async Task<ReindexResult> ReindexAsync(ReindexWorkItem workItem, WorkItemContext context, int startProgress = 0, int endProgress = 100, DateTime? startTime = null) {
            const int pageSize = 100;
            const string scroll = "10s";

            var scanResults = await _client.SearchAsync<JObject>(s => s.Index(workItem.OldIndex).AllTypes().Filter(f => startTime.HasValue ? f.Range(r => r.OnField(workItem.TimestampField ?? "_timestamp").Greater(startTime.Value)) : f.MatchAll()).From(0).Take(pageSize).SearchType(SearchType.Scan).Scroll(scroll)).AnyContext();

            if (!scanResults.IsValid || scanResults.ScrollId == null) {
                Logger.Error().Message("Invalid search result: message={0}", scanResults.GetErrorMessage()).Write();
                return new ReindexResult();
            }

            long totalHits = scanResults.Total;
            long completed = 0;
            int page = 0;
            var results = await _client.ScrollAsync<JObject>(scroll, scanResults.ScrollId).AnyContext();
            while (results.Documents.Any()) {
                var bulkDescriptor = new BulkDescriptor();
                foreach (var hit in results.Hits) {
                    var h = hit;
                    // TODO: Add support for doing JObject based schema migrations
                    bulkDescriptor.Index<JObject>(idx => idx.Index(workItem.NewIndex).Type(h.Type).Id(h.Id).Document(h.Source));
                }

                var bulkResponse = await _client.BulkAsync(bulkDescriptor).AnyContext();
                if (!bulkResponse.IsValid) {
                    string message = $"Reindex bulk error: old={workItem.OldIndex} new={workItem.NewIndex} page={page} message={bulkResponse.GetErrorMessage()}";
                    Logger.Warn().Message(message).Write();
                    // try each doc individually so we can see which doc is breaking us
                    foreach (var hit in results.Hits) {
                        var h = hit;
                        var response = await _client.IndexAsync(h.Source, d => d.Index(workItem.NewIndex).Type(h.Type).Id(h.Id)).AnyContext();

                        if (response.IsValid)
                            continue;

                        message = $"Reindex error: old={workItem.OldIndex} new={workItem.NewIndex} id={hit.Id} page={page} message={response.GetErrorMessage()}";
                        Logger.Error().Message(message).Write();
                        throw new ReindexException(response.ConnectionStatus, message);
                    }
                }

                completed += bulkResponse.Items.Count();
                await context.ReportProgressAsync(CalculateProgress(totalHits, completed, startProgress, endProgress), $"Total: {totalHits} Completed: {completed}").AnyContext();
                results = await _client.ScrollAsync<JObject>(scroll, results.ScrollId).AnyContext();
                page++;
            }

            return new ReindexResult {
                Total = totalHits,
                Completed = completed
            };
        }
        
        private class ReindexResult {
            public long Total { get; set; }
            public long Completed { get; set; }
        }
    }
}