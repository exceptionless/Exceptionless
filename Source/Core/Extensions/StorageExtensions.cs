using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Queues.Models;
using Foundatio.Storage;
using NLog.Fluent;

namespace Exceptionless.Core.Extensions {
    public static class StorageExtensions {
        public static async Task<EventPostInfo> GetEventPostAndSetActiveAsync(this IFileStorage storage, string path, CancellationToken cancellationToken = default(CancellationToken)) {
            EventPostInfo eventPostInfo;
            try {
                eventPostInfo = await storage.GetObjectAsync<EventPostInfo>(path, cancellationToken).AnyContext();
                if (eventPostInfo == null)
                    return null;

                if (!await storage.ExistsAsync(path + ".x").AnyContext() && !await storage.SaveFileAsync(path + ".x", new MemoryStream(Encoding.UTF8.GetBytes(String.Empty)), cancellationToken).AnyContext())
                    return null;
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error retrieving event post data \"{0}\".", path).Write();
                return null;
            }

            return eventPostInfo;
        }

        public static async Task<bool> SetNotActiveAsync(this IFileStorage storage, string path, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                return await storage.DeleteFileAsync(path + ".x", cancellationToken).AnyContext();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error deleting work marker \"{0}\".", path + ".x").Write();
            }

            return false;
        }

        public static async Task<bool> CompleteEventPostAsync(this IFileStorage storage, string path, string projectId, DateTime created, bool shouldArchive = true, CancellationToken cancellationToken = default(CancellationToken)) {
            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return true;

            string archivePath = $"archive\\{projectId}\\{created.ToString("yy\\\\MM\\\\dd")}\\{Path.GetFileName(path)}";
            
            try {
                if (shouldArchive) {
                    if (!await storage.RenameFileAsync(path, archivePath, cancellationToken).AnyContext())
                        return false;
                } else {
                    if (!await storage.DeleteFileAsync(path, cancellationToken).AnyContext())
                        return false;
                }
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error archiving event post data \"{0}\".", path).Write();
                return false;
            }

            await storage.SetNotActiveAsync(path, cancellationToken).AnyContext();
            return true;
        }
    }
}
