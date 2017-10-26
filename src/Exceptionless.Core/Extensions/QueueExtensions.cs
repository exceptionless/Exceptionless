using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Utility;

namespace Exceptionless.Core.Extensions {
    public static class QueueExtensions {
        public static Task<string> EnqueueAsync(this IQueue<EventPost> queue, EventPostInfo data, IFileStorage storage) {
            return EnqueueAsync(queue, data, storage, Settings.Current.EnableArchive);
        }

        public static async Task<string> EnqueueAsync(this IQueue<EventPost> queue, EventPostInfo data, IFileStorage storage, bool shouldArchive, CancellationToken cancellationToken = default) {
            string path;
            if (shouldArchive) {
                var utcNow = SystemClock.UtcNow;
                path = Path.Combine("archive", utcNow.ToString("yy"), utcNow.ToString("MM"), utcNow.ToString("dd"), utcNow.ToString("HH"), data.ProjectId, $"{Guid.NewGuid():N}.json");
            } else {
                path = Path.Combine("q", $"{Guid.NewGuid():N}.json");
            }

            if (!await storage.SaveObjectAsync(path, data, cancellationToken).AnyContext())
                return null;

            return await queue.EnqueueAsync(new EventPost {
                FilePath = path,
                ShouldArchive = shouldArchive
            }).AnyContext();
        }
    }
}