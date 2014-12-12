using System;
using System.IO;
using Exceptionless.Core.Models;
using Exceptionless.Core.Storage;
using Exceptionless.Json;
using NLog.Fluent;

namespace Exceptionless.Core.Extensions {
    public static class StorageExtensions {
        public static bool SaveObject<T>(this IFileStorage storage, string path, T data) {
            return storage.SaveFile(path, JsonConvert.SerializeObject(data));
        }

        public static T GetObject<T>(this IFileStorage storage, string path) {
            string json = storage.GetFileContents(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static EventPost GetEventPostAndSetActive(this IFileStorage storage, string path) {
            EventPost eventPost = null;
            try {
                eventPost = storage.GetObject<EventPost>(path);
                if (eventPost == null)
                    return null;

                if (!storage.Exists(path + ".x") && !storage.SaveFile(path + ".x", String.Empty))
                    return null;
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error retrieving event post data \"{0}\".", path).Write();
                return null;
            }

            return eventPost;
        }

        public static bool SetNotActive(this IFileStorage storage, string path) {
            try {
                return storage.DeleteFile(path + ".x");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error deleting work marker \"{0}\".", path + ".x").Write();
            }

            return false;
        }

        public static bool ArchiveEventPost(this IFileStorage storage, string path, string projectId, DateTime created) {
            // don't move files that are already in the archive
            if (path.StartsWith("archive"))
                return true;

            string archivePath = String.Format("archive\\{0}\\{1}\\{2}", projectId, created.ToString("yy\\\\MM\\\\dd"), Path.GetFileName(path));
            
            try {
                if (!storage.RenameFile(path, archivePath))
                    return false;
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error archiving event post data \"{0}\".", path).Write();
                return false;
            }

            try {
                storage.DeleteFile(path + ".x");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Error deleting work marker \"{0}\".", path + ".x").Write();
            }

            return true;
        }
    }
}
