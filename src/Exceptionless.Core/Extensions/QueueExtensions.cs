using System;
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
            if (shouldArchive)
                path = $"archive\\{SystemClock.UtcNow:yy\\\\MM\\\\dd\\\\HH}\\{data.ProjectId}\\{Guid.NewGuid():N}.json";
            else
                path = $"q\\{Guid.NewGuid():N}.json";

            if (!await storage.SaveObjectAsync(path, data, cancellationToken).AnyContext())
                return null;

            return await queue.EnqueueAsync(new EventPost {
                FilePath = path,
                ShouldArchive = shouldArchive
            }).AnyContext();
        }
    }
}