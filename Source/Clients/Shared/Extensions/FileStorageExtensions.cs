using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Extensions {
    public static class FileStorageExtensions {
        private static readonly object _lockObject = new object();

        public static void Enqueue(this IObjectStorage storage, string queueName, Event ev) {
            storage.SaveObject(String.Concat(queueName, "\\q\\", Guid.NewGuid().ToString("N"), ".0.json"), ev);
        }

        public static void CleanupQueueFiles(this IObjectStorage storage, string queueName, TimeSpan? maxAge = null, int? maxAttempts = 3) {
            if (!maxAge.HasValue)
                maxAge = TimeSpan.FromDays(1);

            if (!maxAttempts.HasValue || maxAttempts.Value <= 0)
                maxAttempts = 3;

            foreach (var file in storage.GetObjectList(queueName + "\\q\\*", 500).ToList()) {
                if (file.Created < DateTime.Now.Subtract(maxAge.Value))
                    storage.DeleteObject(file.Path);
                if (GetAttempts(file) >= maxAttempts)
                    storage.DeleteObject(file.Path);
            }
        }

        public static ICollection<ObjectInfo> GetQueueFiles(this IObjectStorage storage, string queueName, int? limit = null, DateTime? maxCreatedDate = null) {
            return storage.GetObjectList(queueName + "\\q\\*.json", limit, maxCreatedDate).OrderByDescending(f => f.Created).ToList();
        }

        public static bool IncrementAttempts(this IObjectStorage storage, ObjectInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length < 3)
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            int version;
            if (!Int32.TryParse(parts[1], out version))
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            version++;
            string newpath = String.Join(".", parts[0], version, parts[2]);
            if (parts.Length == 4)
                newpath += "." + parts[3];

            string originalPath = info.Path;
            info.Path = newpath;

            return storage.RenameObject(originalPath, newpath);
        }

        public static int GetAttempts(this ObjectInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length != 3)
                return 0;

            int attempts;
            return !Int32.TryParse(parts[1], out attempts) ? 0 : attempts;
        }

        public static bool LockFile(this IObjectStorage storage, ObjectInfo info) {
            if (info.Path.EndsWith(".x"))
                return false;

            string lockedPath = String.Concat(info.Path, ".x");

            bool success = storage.RenameObject(info.Path, lockedPath);
            if (!success)
                return false;

            info.Path = lockedPath;
            return true;
        }

        public static bool ReleaseFile(this IObjectStorage storage, ObjectInfo info) {
            if (!info.Path.EndsWith(".x"))
                return false;

            string path = info.Path.Substring(0, info.Path.Length - 2);

            bool success = storage.RenameObject(info.Path, path);
            if (!success)
                return false;

            info.Path = path;
            return true;
        }

        public static void ReleaseStaleLocks(this IObjectStorage storage, string queueName, TimeSpan? maxLockAge = null) {
            if (!maxLockAge.HasValue)
                maxLockAge = TimeSpan.FromMinutes(60);

            foreach (var file in storage.GetObjectList(queueName + "\\q\\*.x", 500).ToList().Where(f => f.Modified < DateTime.Now.Subtract(maxLockAge.Value)))
                storage.ReleaseFile(file);
        }

        public static IList<Tuple<ObjectInfo, Event>> GetEventBatch(this IObjectStorage storage, string queueName, IJsonSerializer serializer, int batchSize = 50, DateTime? maxCreatedDate = null) {
            var events = new List<Tuple<ObjectInfo, Event>>();

            lock (_lockObject) {
                foreach (var file in storage.GetQueueFiles(queueName, batchSize * 5, maxCreatedDate)) {
                    if (!storage.LockFile(file))
                        continue;

                    try {
                        storage.IncrementAttempts(file);
                    } catch {}

                    try {
                        var ev = storage.GetObject<Event>(file.Path);
                        events.Add(Tuple.Create(file, ev));
                        if (events.Count == batchSize)
                            break;

                    } catch {}
                }

                return events;
            }
        }

        public static void DeleteFiles(this IObjectStorage storage, IEnumerable<ObjectInfo> files) {
            foreach (var file in files)
                storage.DeleteObject(file.Path);
        }

        public static void DeleteBatch(this IObjectStorage storage, IList<Tuple<ObjectInfo, Event>> batch) {
            foreach (var item in batch)
                storage.DeleteObject(item.Item1.Path);
        }

        public static void ReleaseBatch(this IObjectStorage storage, IList<Tuple<ObjectInfo, Event>> batch) {
            foreach (var item in batch)
                storage.ReleaseFile(item.Item1);
        }
    }
}
