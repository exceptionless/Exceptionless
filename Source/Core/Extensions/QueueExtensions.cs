using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Storage;

namespace Exceptionless.Core.Extensions {
    public static class QueueExtensions {
        public static async Task<string> EnqueueAsync(this IQueue<EventPost> queue, EventPostInfo data, IFileStorage storage, bool shouldArchive = true, CancellationToken cancellationToken = default(CancellationToken)) {
            string path = String.Format("q\\{0}.json", Guid.NewGuid().ToString("N"));
            if (!await storage.SaveObjectAsync(path, data, cancellationToken).AnyContext())
                return null;

            return await queue.EnqueueAsync(new EventPost {
                FilePath = path,
                ShouldArchive = shouldArchive
            }).AnyContext();
        }
    }
}
