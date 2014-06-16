using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Extensions {
    public static class FileStorageExtensions {
        public static void Enqueue(this IFileStorage storage, Event ev, IJsonSerializer serializer) {
            storage.SaveObject(String.Concat("q\\", Guid.NewGuid().ToString("N"), ".0.json"), ev, serializer);
        }

        public static void SaveObject<T>(this IFileStorage storage, string path, T data, IJsonSerializer serializer) {
            storage.SaveFile(path, serializer.Serialize(data));
        }

        public static T GetObject<T>(this IFileStorage storage, string path, IJsonSerializer serializer) {
            string json = storage.GetFileContents(path);
            return serializer.Deserialize<T>(json);
        }

        public static void DeleteOldQueueFiles(this IFileStorage storage, DateTime? maxAge = null) {
            if (!maxAge.HasValue)
                maxAge = DateTime.Now.Subtract(TimeSpan.FromDays(1));

            foreach (var file in storage.GetFileList("q\\*").ToList().Where(f => f.Created < maxAge).ToList())
                storage.DeleteFile(file.Path);
        }

        public static ICollection<FileInfo> GetQueueFiles(this IFileStorage storage, int? limit = null) {
            var files = storage.GetFileList("q\\*.json").OrderByDescending(f => f.Created);
            return limit.HasValue ? files.Take(limit.Value).ToList() : files.ToList();
        }

        public static void IncrementAttempts(this IFileStorage storage, FileInfo info) {
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

            storage.RenameFile(originalPath, newpath);
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

            storage.RenameFile(info.Path, lockedPath);
            info.Path = lockedPath;
            return true;
        }

        public static bool ReleaseFile(this IFileStorage storage, FileInfo info) {
            if (!info.Path.EndsWith(".x"))
                return false;

            string path = info.Path.Substring(0, info.Path.Length - 2);

            storage.RenameFile(info.Path, path);
            info.Path = path;
            return true;
        }

        public static void ReleaseOldLocks(this IFileStorage storage) {
            foreach (var file in storage.GetFileList("q\\*.x").ToList().Where(f => f.Modified < DateTime.Now.Subtract(TimeSpan.FromMinutes(60))))
                storage.ReleaseFile(file);
        }

        public static IList<Tuple<FileInfo, Event>> GetEventBatch(this IFileStorage storage, IJsonSerializer serializer, int batchSize = 20) {
            var events = new List<Tuple<FileInfo, Event>>();

            foreach (var file in (storage.GetQueueFiles())) {
                if (!storage.LockFile(file))
                    continue;

                try {
                    storage.IncrementAttempts(file);
                } catch { }

                try {
                    var ev = storage.GetObject<Event>(file.Path, serializer);
                    events.Add(Tuple.Create(file, ev));
                    if (events.Count == batchSize)
                        break;

                } catch { }
            }

            return events;
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
