using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using Nest;
using NLog.Fluent;
using OldModels = Exceptionless.EventMigration.Models;
#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class StackMigrationJob : MigrationJobBase {
        public StackMigrationJob(IElasticClient elasticClient, EventUpgraderPluginManager eventUpgraderPluginManager, IValidator<Stack> stackValidator, IValidator<PersistentEvent> eventValidator, IGeoIPResolver geoIpResolver, ILockProvider lockProvider, ICacheClient cache)
            : base(elasticClient, eventUpgraderPluginManager, stackValidator, eventValidator, geoIpResolver, lockProvider, cache) {
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("StackMigrationJob");
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            OutputPublicIp();

            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var errorStackCollection = GetErrorStackCollection();

            var mostRecentStack = _cache.Get<string>("migration-stackid");
            var query = mostRecentStack != null ? Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(mostRecentStack)) : Query.Null;
            var stacks = errorStackCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(_batchSize).ToList();
            while (stacks.Count > 0) {
                stacks.ForEach(s => {
                    s.Type = s.SignatureInfo != null && s.SignatureInfo.ContainsKey("HttpMethod") && s.SignatureInfo.ContainsKey("Path") ? "404" : "error";

                    if (s.Tags != null)
                        s.Tags.RemoveWhere(t => String.IsNullOrEmpty(t) || t.Length > 255);

                    if (s.Title != null && s.Title.Length > 1000)
                        s.Title = s.Title.Truncate(1000);
                });

                Log.Info().Message("Migrating stacks {0:N0} total {1:N0}/s...", total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();
                try {
                    // TODO: Comment out sendNotifications:false. When I was importing the stacks. I was getting an error where RunPeriod was erroring out due to a null message.
                    _stackRepository.Add(stacks, sendNotification: false);
                } catch (Exception ex) {
                    Debugger.Break();
                    Log.Error().Exception(ex).Message("An error occurred while migrating stacks").Write();
                    return JobResult.FromException(ex, String.Format("An error occurred while migrating stacks: {0}", ex.Message));
                }

                var lastId = stacks.Last().Id;
                _cache.Set("migration-stackid", lastId);
                stacks = errorStackCollection.Find(Query.GT(ErrorStackFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorStackFieldNames.Id)).SetLimit(_batchSize).ToList();
                total += stacks.Count;
            }

            Log.Info().Message("Finished migrating stacks.").Write();
            return JobResult.Success;
        }
    }
}