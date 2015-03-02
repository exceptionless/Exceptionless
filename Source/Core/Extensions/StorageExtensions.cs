using System;
using System.IO;
using Exceptionless.Core.Queues.Models;
using Foundatio.Storage;
using NLog.Fluent;

namespace Exceptionless.Core.Extensions {
    public static class StorageExtensions {
        public static EventPostInfo GetEventPostAndSetActive(this IFileStorage storage, string path) {
            EventPostInfo eventPostInfo = null;
            try {
                eventPostInfo = storage.GetObject<EventPostInfo>(path);
                if (eventPostInfo == null)
                    return null;

                if (!storage.Exists(path + ".x") && !storage.SaveFile(path + ".x", String.Empty))
                    return null;
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error retrieving event post data \"{0}\".", path).Write();
                return null;
            }

            return eventPostInfo;
        }

        public static bool SetNotActive(this IFileStorage storage, string path) {
            try {
                return storage.DeleteFile(path + ".x");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error deleting work marker \"{0}\".", path + ".x").Write();
            }

            return false;
        }

        public static bool CompleteEventPost(this IFileStorage storage, string path, string projectId, DateTime created, bool shouldArchive = true) {
            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return true;

            string archivePath = String.Format("archive\\{0}\\{1}\\{2}", projectId, created.ToString("yy\\\\MM\\\\dd"), Path.GetFileName(path));
            
            try {
                if (shouldArchive) {
                    if (!storage.RenameFile(path, archivePath))
                        return false;
                } else {
                    if (!storage.DeleteFile(path))
                        return false;
                }
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error archiving event post data \"{0}\".", path).Write();
                return false;
            }

            storage.SetNotActive(path);

            return true;
        }
    }
}
