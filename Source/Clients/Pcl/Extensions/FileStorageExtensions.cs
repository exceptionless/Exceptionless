using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Extensions {
    public static class FileStorageExtensions {
        public static Task SaveFileAsync(this IFileStorage storage, string path, string contents) {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents))) {
                return storage.SaveFileAsync(path, stream);
            }
        }

        public async static Task<T> DeserializeFileAsync<T>(this IFileStorage storage, string path, IJsonSerializer serializer) {
            using (var reader = new StreamReader(await storage.GetFileContentsAsync(path))) {
                return serializer.Deserialize<T>(await reader.ReadToEndAsync());
            }
        }

        public async static Task DeleteOldQueueFilesAsync(this IFileStorage storage, DateTime? maxAge = null) {
            if (!maxAge.HasValue)
                maxAge = DateTime.Now.Subtract(TimeSpan.FromDays(1));

            foreach (var file in (await storage.GetFileListAsync("q\\*")).Where(f => f.Created < maxAge))
                await storage.DeleteFileAsync(file.Path);
        }

        public async static Task<ICollection<FileInfo>> GetQueueFilesAsync(this IFileStorage storage, int? limit = null) {
            var files = (await storage.GetFileListAsync("q\\*.json")).OrderByDescending(f => f.Created);
            return limit.HasValue ? files.Take(limit.Value).ToList() : files.ToList();
        }

        public async static Task IncrementAttemptsAsync(this IFileStorage storage, FileInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length < 3)
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            int version = 0;
            if (!Int32.TryParse(parts[1], out version))
                throw new ArgumentException(String.Format("Path \"{0}\" must contain the number of attempts.", info.Path));

            version++;
            string newpath = String.Concat(parts[0], version, parts[2]);
            if (parts.Length == 4)
                newpath += parts[4];

            info.Path = newpath;

            await storage.RenameFileAsync(info.Path, newpath);
        }

        public static int GetAttempts(this FileInfo info) {
            string[] parts = info.Path.Split('.');
            if (parts.Length != 3)
                return 0;

            int attempts = 0;
            return !Int32.TryParse(parts[1], out attempts) ? 0 : attempts;
        }

        public async static Task<bool> LockFile(this IFileStorage storage, FileInfo info) {
            if (info.Path.EndsWith(".x"))
                return false;

            string lockedPath = String.Concat(info.Path, ".x");

            try {
                await storage.RenameFileAsync(info.Path, lockedPath);
            } catch {
                return false;
            }
            info.Path = lockedPath;

            return true;
        }

        public async static Task<bool> ReleaseFileAsync(this IFileStorage storage, FileInfo info) {
            if (!info.Path.EndsWith(".x"))
                return false;

            string path = info.Path.Substring(0, info.Path.Length - 2);

            try {
                await storage.RenameFileAsync(info.Path, path);
            } catch {
                return false;
            }
            info.Path = path;

            return true;
        }

        public async static Task ReleaseOldLocks(this IFileStorage storage) {
            foreach (var file in (await storage.GetFileListAsync("q\\*.x")).Where(f => f.Modified < DateTime.Now.Subtract(TimeSpan.FromMinutes(60))))
                await storage.ReleaseFileAsync(file);
        }

        public async static Task<IList<Tuple<FileInfo, Event>>> GetEventBatchAsync(this IFileStorage storage, IJsonSerializer serializer, int batchSize = 20) {
            var events = new List<Tuple<FileInfo, Event>>();
            foreach (var file in (await storage.GetQueueFilesAsync())) {
                if (!await storage.LockFile(file))
                    continue;

                try {
                    await storage.IncrementAttemptsAsync(file);
                } catch {}

                try {
                    var ev = await storage.DeserializeFileAsync<Event>(file.Path, serializer);
                    events.Add(Tuple.Create(file, ev));
                    if (events.Count == batchSize)
                        break;

                } catch {}
            }

            return events;
        }

        public static async Task DeleteBatchAsync(this IFileStorage storage, IList<Tuple<FileInfo, Event>> batch) {
            foreach (var item in batch) {
                try {
                    await storage.DeleteFileAsync(item.Item1.Path);
                } catch {}
            }
        }

        public static async Task ReleaseBatchAsync(this IFileStorage storage, IList<Tuple<FileInfo, Event>> batch) {
            foreach (var item in batch) {
                try {
                    await storage.ReleaseFileAsync(item.Item1);
                } catch {}
            }
        }
    }
}
