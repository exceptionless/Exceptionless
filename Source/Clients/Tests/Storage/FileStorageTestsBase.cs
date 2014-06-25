using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Helpers;
using Exceptionless;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Storage;
using Xunit;

namespace Client.Tests.Storage {
    public abstract class FileStorageTestsBase {
        protected abstract IFileStorage GetStorage();

        [Fact]
        public void CanManageFiles() {
            Reset();

            IFileStorage storage = GetStorage();
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

        [Fact]
        public void CanManageQueue() {
            Reset();

            IFileStorage storage = GetStorage();
            const string queueName = "test";

            IJsonSerializer serializer = new DefaultJsonSerializer();
            var ev = new Event { Type = Event.KnownTypes.Log, Message = "test" };
            storage.Enqueue(queueName, ev, serializer);
            storage.SaveFile("test.txt", "test");
            Assert.True(storage.GetFileList().Any(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("0.json")));
            Assert.Equal(2, storage.GetFileList().Count());

            Assert.True(storage.GetQueueFiles(queueName).All(f => f.Path.EndsWith("0.json")));
            Assert.Equal(1, storage.GetQueueFiles(queueName).Count());

            storage.DeleteFile("test.txt");
            Assert.Equal(1, storage.GetFileList().Count());

            Assert.True(storage.LockFile(storage.GetFileList().FirstOrDefault()));
            Assert.True(storage.GetQueueFiles(queueName).All(f => f.Path.EndsWith("0.json.x")));
            Assert.True(storage.ReleaseFile(storage.GetFileList().FirstOrDefault()));

            var batch = storage.GetEventBatch(queueName, serializer);
            Assert.Equal(1, batch.Count);

            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("1.json.x")));
            Assert.Equal(1, storage.GetFileList().Count());

            Assert.Equal(0, storage.GetQueueFiles(queueName).Count());
            Assert.Equal(0, storage.GetEventBatch(queueName, serializer).Count());

            Assert.False(storage.LockFile(storage.GetFileList().FirstOrDefault()));

            storage.ReleaseBatch(batch);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("1.json")));
            Assert.Equal(1, storage.GetFileList().Count());
            Assert.Equal(1, storage.GetQueueFiles(queueName).Count());

            var file = storage.GetFileList().FirstOrDefault();
            storage.IncrementAttempts(file);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("2.json")));
            storage.IncrementAttempts(file);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("3.json")));

            Assert.True(storage.LockFile(file));
            Assert.NotNull(file);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("3.json.x")));
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            storage.ReleaseStaleLocks(queueName, TimeSpan.Zero);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("3.json")));

            batch = storage.GetEventBatch(queueName, serializer);
            Assert.Equal(1, batch.Count);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith(queueName + "\\q\\") && f.Path.EndsWith("4.json.x")));
            storage.DeleteBatch(batch);
            Assert.Equal(0, storage.GetQueueFiles(queueName).Count());

            ev = new Event { Type = Event.KnownTypes.Log, Message = "test" };
            storage.Enqueue(queueName, ev, serializer);
            file = storage.GetFileList().FirstOrDefault();
            Assert.NotNull(file);
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
            storage.CleanupQueueFiles(queueName, TimeSpan.Zero);
            Assert.Equal(0, storage.GetQueueFiles(queueName).Count());
        }

        private void Reset() {
            var storage = GetStorage();
            var files = storage.GetFileList();
            if (files.Any())
                Debug.WriteLine("Got files");
            else
                Debug.WriteLine("No files");
            storage.DeleteFiles(storage.GetFileList());
            Assert.Equal(0, storage.GetFileList().Count());
        }

        [Fact]
        public void CanConcurrentlyManageFiles() {
            Reset();

            IFileStorage storage = GetStorage();
            IJsonSerializer serializer = new DefaultJsonSerializer();
            const string queueName = "test";

            Parallel.For(0, 25, i => {
                var ev = new Event {
                    Type = Event.KnownTypes.Log,
                    Message = "test" + i
                };
                storage.Enqueue(queueName, ev, serializer);
            });
            Assert.Equal(25, storage.GetFileList().Count());
            var working = new ConcurrentDictionary<string, object>();

            Parallel.For(0, 50, i => {
                var fileBatch = storage.GetEventBatch(queueName, serializer, 2);
                foreach (var f in fileBatch) {
                    if (working.ContainsKey(f.Item1.Path))
                        Debug.WriteLine(f.Item1.Path);
                    Assert.False(working.ContainsKey(f.Item1.Path));
                    working.TryAdd(f.Item1.Path, null);
                }

                if (RandomHelper.GetBool()) {
                    object o;
                    foreach (var f in fileBatch)
                        working.TryRemove(f.Item1.Path, out o);
                    storage.ReleaseBatch(fileBatch);
                } else {
                    storage.DeleteBatch(fileBatch);
                }
            });
            Assert.Equal(25, working.Count + storage.GetQueueFiles(queueName).Count);
        }
    }
}
