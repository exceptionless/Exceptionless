using System;
using System.Collections.Generic;
using System.IO;
using Exceptionless.Core.Models;
using Foundatio.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
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

        public static void DeleteFiles(this IFileStorage storage, IEnumerable<FileSpec> files) {
            foreach (var file in files)
                storage.DeleteFile(file.Path);
        }

        public static FileSpec ToFileInfo(this CloudBlockBlob blob) {
            return new FileSpec {
                Path = blob.Name,
                Size = blob.Properties.Length,
                Modified = blob.Properties.LastModified.HasValue ? blob.Properties.LastModified.Value.UtcDateTime : DateTime.MinValue,
                Created = blob.Properties.LastModified.HasValue ? blob.Properties.LastModified.Value.UtcDateTime : DateTime.MinValue
            };
        }
    }
}
