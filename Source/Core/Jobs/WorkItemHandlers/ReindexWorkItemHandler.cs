using System;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Nest;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using SimpleInjector;


namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ReindexWorkItemHandler : IWorkItemHandler {
        private readonly IElasticClient _client;
        private readonly Container _container;

        public ReindexWorkItemHandler(IElasticClient client, Container container) {
            _client = client;
            _container = container;
        }

        public Task HandleItem(WorkItemContext context) {
            var workItem = context.GetData<ReindexWorkItem>();

            Log.Info().Message("Received reindex work item for new index {0}", workItem.NewIndex).Write();
            var startTime = DateTime.UtcNow.AddSeconds(-1);
            context.ReportProgress(0, "Starting reindex...");
            var result = Reindex(workItem, context, 0, 90, workItem.StartUtc);
            context.ReportProgress(90, String.Format("Total: {0} Completed: {1}", result.Total, result.Completed));

            // TODO: Check to make sure the docs have been added to the new index before changing alias

            if (!String.IsNullOrEmpty(workItem.Alias)) {
                _client.Alias(x => x.Remove(a => a.Alias(workItem.Alias).Index(workItem.OldIndex)).Add(a => a.Alias(workItem.Alias).Index(workItem.NewIndex)));
                context.ReportProgress(98, String.Format("Updated alias: {0} Remove: {1} Add: {2}", workItem.Alias, workItem.OldIndex, workItem.NewIndex));
            }

            _client.Refresh();
            var secondPassResult = Reindex(workItem, context, 90, 98, startTime);
            context.ReportProgress(98, String.Format("Total: {0} Completed: {1}", secondPassResult.Total, secondPassResult.Completed));

            if (workItem.DeleteOld) {
                _client.Refresh();
                long newDocCount = _client.Count(d => d.Index(workItem.OldIndex)).Count;
                long oldDocCount = _client.Count(d => d.Index(workItem.OldIndex)).Count;
                context.ReportProgress(98, String.Format("Old Docs: {0} New Docs: {1}", oldDocCount, newDocCount));
                if (newDocCount >= oldDocCount)
                    _client.DeleteIndex(d => d.Index(workItem.OldIndex));
                context.ReportProgress(98, String.Format("Deleted index: {0}", workItem.OldIndex));
            }
            context.ReportProgress(100);

            return TaskHelper.Completed();
        }

        private ReindexResult Reindex(ReindexWorkItem workItem, WorkItemContext context, int startProgress = 0, int endProgress = 100, DateTime? startTime = null) {
            const int pageSize = 100;
            const string scroll = "10s";

            var scanResults = _client.Search<JObject>(s => s.Index(workItem.OldIndex).AllTypes().Filter(f => startTime.HasValue ? f.Range(r => r.OnField(workItem.TimestampField ?? "_timestamp").Greater(startTime.Value)) : f.MatchAll()).From(0).Take(pageSize).SearchType(SearchType.Scan).Scroll(scroll));

            if (!scanResults.IsValid || scanResults.ScrollId == null) {
                Log.Error().Message("Invalid search result: message={0}", scanResults.GetErrorMessage()).Write();
                return new ReindexResult();
            }

            long totalHits = scanResults.Total;
            long completed = 0;
            int page = 0;
            var results = _client.Scroll<JObject>(scroll, scanResults.ScrollId);
            while (results.Documents.Any()) {
                var bulkDescriptor = new BulkDescriptor();
                foreach (var hit in results.Hits) {
                    var h = hit;
                    // TODO: Add support for doing JObject based schema migrations
                    bulkDescriptor.Index<JObject>(idx => idx.Index(workItem.NewIndex).Type(h.Type).Id(h.Id).Document(h.Source));
                }

                var bulkResponse = _client.Bulk(bulkDescriptor);
                if (!bulkResponse.IsValid) {
                    string message = String.Format("Reindex bulk error: old={0} new={1} page={2} message={3}", workItem.OldIndex, workItem.NewIndex, page, bulkResponse.GetErrorMessage());
                    Log.Warn().Message(message).Write();
                    // try each doc individually so we can see which doc is breaking us
                    foreach (var hit in results.Hits) {
                        var h = hit;
                        var response = _client.Index<JObject>(h.Source, d => d.Index(workItem.NewIndex).Type(h.Type).Id(h.Id));

                        if (response.IsValid)
                            continue;

                        message = String.Format("Reindex error: old={0} new={1} id={2} page={3} message={4}", workItem.OldIndex, workItem.NewIndex, hit.Id, page, response.GetErrorMessage());
                        Log.Error().Message(message).Write();
                        throw new ReindexException(response.ConnectionStatus, message);
                    }
                }

                completed += bulkResponse.Items.Count();
                context.ReportProgress(CalculateProgress(totalHits, completed, startProgress, endProgress), String.Format("Total: {0} Completed: {1}", totalHits, completed));
                results = _client.Scroll<JObject>(scroll, results.ScrollId);
                page++;
            }

            return new ReindexResult {
                Total = totalHits,
                Completed = completed
            };
        }

        private int CalculateProgress(long total, long completed, int startProgress = 0, int endProgress = 100) {
            return startProgress + (int)((100 * (double)completed / total) * (((double)endProgress - startProgress) / 100));
        }

        private class ReindexResult {
            public long Total { get; set; }
            public long Completed { get; set; }
        }
    }
}