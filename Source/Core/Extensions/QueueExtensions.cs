using System;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Storage;
using NLog.Fluent;

namespace Exceptionless.Core.Extensions {
    public static class QueueExtensions {
        public static string Enqueue(this IQueue<EventPostFileInfo> queue, EventPost data, IFileStorage storage, bool shouldArchive = true) {
            string path = String.Format("q\\{0}.json", Guid.NewGuid().ToString("N"));
            try {
                if (!storage.SaveObject(path, data))
                    return null;
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error saving event post data to storage.").Write();
            }

            return queue.Enqueue(new EventPostFileInfo {
                FilePath = path,
                ShouldArchive = shouldArchive
            });
        }
    }
}
