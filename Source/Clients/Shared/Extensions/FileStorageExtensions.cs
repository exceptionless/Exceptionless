using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Extensions {
    public static class FileStorageExtensions {
        private static readonly object _lockObject = new object();

        public static void Enqueue(this IFileStorage storage, string queueName, Event ev, IJsonSerializer serializer) {
            storage.SaveObject(String.Concat(queueName, "\\q\\", Guid.NewGuid().ToString("N"), ".0.json"), ev, serializer);
        }

        public static void SaveObject<T>(this IFileStorage storage, string path, T data, IJsonSerializer serializer) {
            storage.SaveFile(path, serializer.Serialize(data));
        }

        public static T GetObject<T>(this IFileStorage storage, string path, IJsonSerializer serializer) {
            string json = storage.GetFileContents(path);
            return serializer.Deserialize<T>(json);
        }

        public static void CleanupQueueFiles(this IFileStorage storage, string queueName, TimeSpan? maxAge = null, int? maxAttempts = 3) {
            if (!maxAge.HasValue)
                maxAge = TimeSpan.FromDays(1);

            foreach (var file in storage.GetFileList(queueName + "\\q\\*", 500).ToList()) {
                if (file.Created < DateTime.Now.Subtract(maxAge.Value))
                    storage.DeleteFile(file.Path);
                if (GetAttempts(file) >= 3)
                    storage.DeleteFile(file.Path);
            }
        }

        public static ICollection<FileInfo> GetQueueFiles(this IFileStorage storage, string queueName, int? limit = null, DateTime? maxCreatedDate = null) {
            return storage.GetFileList(queueName + "\\q\\*.json", limit, maxCreatedDate).OrderByDescending(f => f.Created).ToList();
        }

        public static bool IncrementAttempts(this IFileStorage storage, FileInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length < 3)
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            int version = 0;
            if (!Int32.TryParse(parts[1], out version))
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            version++;
            string newpath = String.Join(".", parts[0], version, parts[2]);
            if (parts.Length == 4)
                newpath += "." + parts[3];

            string originalPath = info.Path;
            info.Path = newpath;

            return storage.RenameFile(originalPath, newpath);
        }

        public static int GetAttempts(this FileInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length != 3)
                return 0;

            int attempts = 0;
            return !Int32.TryParse(parts[1], out attempts) ? 0 : attempts;
        }

        public static bool LockFile(this IFileStorage storage, FileInfo info) {
            if (info.Path.EndsWith(".x"))
                return false;

            string lockedPath = String.Concat(info.Path, ".x");

            bool success = storage.RenameFile(info.Path, lockedPath);
            if (!success)
                return false;

            info.Path = lockedPath;
            return true;
        }

        public static bool ReleaseFile(this IFileStorage storage, FileInfo info) {
            if (!info.Path.EndsWith(".x"))
                return false;

            string path = info.Path.Substring(0, info.Path.Length - 2);

            bool success = storage.RenameFile(info.Path, path);
            if (!success)
                return false;

            info.Path = path;
            return true;
        }

        public static void ReleaseStaleLocks(this IFileStorage storage, string queueName, TimeSpan? maxLockAge = null) {
            if (!maxLockAge.HasValue)
                maxLockAge = TimeSpan.FromMinutes(60);

            foreach (var file in storage.GetFileList(queueName + "\\q\\*.x", 500).ToList().Where(f => f.Modified < DateTime.Now.Subtract(maxLockAge.Value)))
                storage.ReleaseFile(file);
        }

        public static IList<Tuple<FileInfo, Event>> GetEventBatch(this IFileStorage storage, string queueName, IJsonSerializer serializer, int batchSize = 50, DateTime? maxCreatedDate = null) {
            var events = new List<Tuple<FileInfo, Event>>();

            lock (_lockObject) {
                foreach (var file in storage.GetQueueFiles(queueName, batchSize * 5, maxCreatedDate)) {
                    if (!storage.LockFile(file))
                        continue;

                    try {
                        storage.IncrementAttempts(file);
                    } catch {}

                    try {
                        var ev = storage.GetObject<Event>(file.Path, serializer);
                        events.Add(Tuple.Create(file, ev));
                        if (events.Count == batchSize)
                            break;

                    } catch {}
                }

                return events;
            }
        }

        public static void DeleteFiles(this IFileStorage storage, IEnumerable<FileInfo> files) {
            foreach (var file in files)
                storage.DeleteFile(file.Path);
        }

        public static void DeleteBatch(this IFileStorage storage, IList<Tuple<FileInfo, Event>> batch) {
            foreach (var item in batch)
                storage.DeleteFile(item.Item1.Path);
        }

        public static void ReleaseBatch(this IFileStorage storage, IList<Tuple<FileInfo, Event>> batch) {
            foreach (var item in batch)
                storage.ReleaseFile(item.Item1);
        }
    }
}
