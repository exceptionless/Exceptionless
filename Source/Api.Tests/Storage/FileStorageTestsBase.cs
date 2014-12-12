using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Storage;
using Xunit;

namespace Exceptionless.Api.Tests.Storage {
    public abstract class FileStorageTestsBase {
        protected abstract IFileStorage GetStorage();

        [Fact]
        public void CanManageFiles() {
            Reset();

            IFileStorage storage = GetStorage();
            if (storage == null)
                return;

            storage.SaveFile("test.txt", "test");
            Assert.Equal(1, storage.GetFileList().Count());
            var file = storage.GetFileList().FirstOrDefault();
            Assert.NotNull(file);
            Assert.Equal("test.txt", file.Path);
            string content = storage.GetFileContents("test.txt");
            Assert.Equal("test", content);
            storage.RenameFile("test.txt", "new.txt");
            Assert.True(storage.GetFileList().Any(f => f.Path == "new.txt"));
            storage.DeleteFile("new.txt");
            Assert.Equal(0, storage.GetFileList().Count());
        }

        protected void Reset() {
            var storage = GetStorage();
            if (storage == null)
                return;

            var files = storage.GetFileList().ToList();
            if (files.Any())
                Debug.WriteLine("Got files");
            else
                Debug.WriteLine("No files");
            storage.DeleteFiles(files);
            Assert.Equal(0, storage.GetFileList().Count());
        }

        [Fact]
        public void CanConcurrentlyManageFiles() {
            Reset();

            IFileStorage storage = GetStorage();
            if (storage == null)
                return;

            const string queueFolder = "q";
            const string archiveFolder = "archive";
            var queueItems = new BlockingCollection<int>();

            Parallel.For(0, 25, i => {
                var ev = new EventPost {
                    ApiVersion = 2,
                    CharSet = "utf8",
                    ContentEncoding = "application/json",
                    Data = Encoding.UTF8.GetBytes("{}"),
                    IpAddress = "127.0.0.1",
                    MediaType = "gzip",
                    ProjectId = i.ToString(),
                    UserAgent = "test"
                };
                storage.SaveObject(Path.Combine(queueFolder, i + ".json"), ev);
                queueItems.Add(i);
            });
            Assert.Equal(25, storage.GetFileList().Count());

            Parallel.For(0, 50, i => {
                string path = Path.Combine(queueFolder, queueItems.Random() + ".json");
                var eventPost = storage.GetEventPostAndSetActive(Path.Combine(queueFolder, RandomHelper.GetRange(0, 25) + ".json"));
                if (eventPost == null)
                    return;

                if (RandomHelper.GetBool()) {
                    storage.CompleteEventPost(path, eventPost.ProjectId, DateTime.UtcNow, true);
                } else
                    storage.SetNotActive(path);
            });
        }
    }
}
