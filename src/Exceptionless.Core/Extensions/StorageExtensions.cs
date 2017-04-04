using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;
using Foundatio.Logging;
using Foundatio.Storage;

namespace Exceptionless.Core.Extensions {
    public static class StorageExtensions {
        public static async Task<EventPostInfo> GetEventPostAndSetActiveAsync(this IFileStorage storage, string path, ILogger logger, CancellationToken cancellationToken = default(CancellationToken)) {
            if (String.IsNullOrEmpty(path))
                return null;

            EventPostInfo eventPostInfo;
            try {
                eventPostInfo = await storage.GetObjectAsync<EventPostInfo>(path, cancellationToken).AnyContext();
            } catch (Exception ex) {
                logger.Error(ex, "Error retrieving event post data \"{0}\".", path);
                return null;
            }

            return eventPostInfo;
        }

        public static Task<bool> CompleteEventPostAsync(this IFileStorage storage, string path, string projectId, DateTime created, ILogger logger, bool shouldArchive = true) {
            if (String.IsNullOrEmpty(path))
                return Task.FromResult(false);

            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return Task.FromResult(true);

            string archivePath = $"archive\\{created:yy\\\\MM\\\\dd\\\\HH}\\{projectId}\\{Path.GetFileName(path)}";
            try {
                if (shouldArchive)
                    return storage.RenameFileAsync(path, archivePath);

                return storage.DeleteFileAsync(path);
            } catch (Exception ex) {
                logger.Error(ex, "Error archiving event post data \"{0}\".", path);
                return Task.FromResult(false);
            }
        }
    }
}
