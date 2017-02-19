using System;
using System.IO;
using System.Text;
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
                if (eventPostInfo == null)
                    return null;

                if (cancellationToken.IsCancellationRequested)
                    return null;

                if (!await storage.ExistsAsync(path + ".x").AnyContext() && !await storage.SaveFileAsync(path + ".x", new MemoryStream(Encoding.UTF8.GetBytes(String.Empty))).AnyContext())
                    return null;
            } catch (Exception ex) {
                logger.Error(ex, "Error retrieving event post data \"{0}\".", path);
                return null;
            }

            return eventPostInfo;
        }

        public static async Task<bool> SetNotActiveAsync(this IFileStorage storage, string path, ILogger logger) {
            if (String.IsNullOrEmpty(path))
                return false;

            try {
                return await storage.DeleteFileAsync(path + ".x").AnyContext();
            } catch (Exception ex) {
                logger.Error(ex, "Error deleting work marker \"{0}\".", path + ".x");
            }

            return false;
        }

        public static async Task<bool> CompleteEventPostAsync(this IFileStorage storage, string path, string projectId, DateTime created, ILogger logger, bool shouldArchive = true) {
            if (String.IsNullOrEmpty(path))
                return false;

            await storage.SetNotActiveAsync(path, logger).AnyContext();

            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return true;

            string archivePath = $"archive\\{created:yy\\\\MM\\\\dd\\\\HH}\\{projectId}\\{Path.GetFileName(path)}";
            try {
                if (shouldArchive && !await storage.ExistsAsync(archivePath).AnyContext()) {
                    if (!await storage.RenameFileAsync(path, archivePath).AnyContext())
                        return false;
                } else {
                    if (!await storage.DeleteFileAsync(path).AnyContext())
                        return false;
                }
            } catch (Exception ex) {
                logger.Error(ex, "Error archiving event post data \"{0}\".", path);
                return false;
            }

            return true;
        }
    }
}
