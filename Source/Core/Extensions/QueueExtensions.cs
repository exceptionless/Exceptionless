using System;
using Exceptionless.Core.Queues.Models;
using Foundatio.Queues;
using Foundatio.Storage;

namespace Exceptionless.Core.Extensions {
    public static class QueueExtensions {
        public static string Enqueue(this IQueue<EventPost> queue, EventPostInfo data, IFileStorage storage, bool shouldArchive = true) {
            string path = String.Format("q\\{0}.json", Guid.NewGuid().ToString("N"));
            if (!storage.SaveObject(path, data))
                return null;

            return queue.Enqueue(new EventPost {
                FilePath = path,
                ShouldArchive = shouldArchive
            });
        }
    }
}
