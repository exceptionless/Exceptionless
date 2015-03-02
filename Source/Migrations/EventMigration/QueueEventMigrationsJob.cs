using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using MongoDB.Driver.Builders;
using Nest;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class QueueEventMigrationsJob : MigrationJobBase {
        private readonly IQueue<EventMigrationBatch> _queue;

        public QueueEventMigrationsJob(IQueue<EventMigrationBatch> queue, IElasticClient elasticClient, EventUpgraderPluginManager eventUpgraderPluginManager, IValidator<Stack> stackValidator, IValidator<PersistentEvent> eventValidator, IGeoIPResolver geoIpResolver, ILockProvider lockProvider, ICacheClient cache) : base(elasticClient, eventUpgraderPluginManager, stackValidator, eventValidator, geoIpResolver, lockProvider, cache) {
            _queue = queue;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var start = GetStartDate();
            while (start < DateTime.UtcNow) {
                Log.Info().Message("Queueing event migration jobs for date range: {0}-{1}", start.ToString("O"), start.AddHours(12).ToString("O")).Write();
                _queue.Enqueue(new EventMigrationBatch { StartTicks = start.Ticks, EndTicks = start.AddHours(12).Ticks });
                _cache.Set("migration-lastqueuedperiod", start.Ticks);
                start = start.AddHours(12);
            }

            Log.Info().Message("Finished queueing event migration jobs").Write();
            return JobResult.Success;
        }

        private DateTime GetStartDate() {
            bool resume = ConfigurationManager.AppSettings.GetBool("Migration:Resume", true);
            if (resume) {
                // Return the last queued day so we can reprocess the last day.
                long ticks;
                if (_cache.TryGet("migration-lastqueuedperiod", out ticks))
                    return new DateTimeOffset(ticks, TimeSpan.Zero).UtcDateTime.Date;

                // Return the day after the last completed day.
                if (_cache.TryGet("migration-completedperiod", out ticks))
                    return new DateTimeOffset(ticks, TimeSpan.Zero).UtcDateTime.Date;
            }

            var errorCollection = GetErrorCollection();
            var firstError = errorCollection.Find(Query.Null).SetSortOrder(SortBy.Ascending(ErrorFieldNames.OccurrenceDate_UTC)).SetLimit(1).FirstOrDefault();
            if (firstError != null && firstError.OccurrenceDate.UtcDateTime.Date > DateTime.UtcNow.Date.SubtractYears(1))
                return firstError.OccurrenceDate.UtcDateTime.Date;

            // Can't find the first error so lets default to exactly one year ago.
            return DateTime.UtcNow.Date.SubtractYears(1);
        }

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("QueueEventMigrationsJob", TimeSpan.Zero);
        }
    }
}