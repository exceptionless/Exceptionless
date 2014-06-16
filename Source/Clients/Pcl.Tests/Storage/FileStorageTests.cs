using System;
using System.Linq;
using Exceptionless;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Storage;
using Xunit;

namespace Pcl.Tests.Storage {
    public class FileStorageTests {
        [Fact]
        public void CanManageFiles() {
            IFileStorage storage = new InMemoryFileStorage();
            storage.SaveFile("test.txt", "test");
            var file = storage.GetFileList().FirstOrDefault();
            Assert.NotNull(file);
            Assert.Equal("test.txt", file.Path);
            Assert.True(DateTime.Now.Subtract(file.Created).TotalSeconds < 5);
            string content = storage.GetFileContents("test.txt");
            Assert.Equal("test", content);
            storage.RenameFile("test.txt", "new.txt");
            Assert.True(storage.GetFileList().Any(f => f.Path == "new.txt"));
            storage.DeleteFile("new.txt");
            Assert.Equal(0, storage.GetFileList().Count());
        }

        [Fact]
        public void CanManageQueue() {
            IFileStorage storage = new InMemoryFileStorage();
            IJsonSerializer serializer = new DefaultJsonSerializer();
            var ev = new Event { Type = Event.KnownTypes.Log, Message = "test" };
            storage.Enqueue(ev, serializer);
            storage.SaveFile("test.txt", "test");
            Assert.True(storage.GetFileList().Any(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("0.json")));
            Assert.Equal(2, storage.GetFileList().Count());

            Assert.True(storage.GetQueueFiles().All(f => f.Path.EndsWith("0.json")));
            Assert.Equal(1, storage.GetQueueFiles().Count());

            storage.DeleteFile("test.txt");
            Assert.Equal(1, storage.GetFileList().Count());

            Assert.True(storage.LockFile(storage.GetFileList().FirstOrDefault()));
            Assert.True(storage.GetQueueFiles().All(f => f.Path.EndsWith("0.json.x")));
            Assert.True(storage.ReleaseFile(storage.GetFileList().FirstOrDefault()));

            var batch = storage.GetEventBatch(serializer);
            Assert.Equal(1, batch.Count);

            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("1.json.x")));
            Assert.Equal(1, storage.GetFileList().Count());

            Assert.Equal(0, storage.GetQueueFiles().Count());
            Assert.Equal(0, storage.GetEventBatch(serializer).Count());

            Assert.False(storage.LockFile(storage.GetFileList().FirstOrDefault()));

            storage.ReleaseBatch(batch);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("1.json")));
            Assert.Equal(1, storage.GetFileList().Count());
            Assert.Equal(1, storage.GetQueueFiles().Count());

            var file = storage.GetFileList().FirstOrDefault();
            storage.IncrementAttempts(file);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("2.json")));
            storage.IncrementAttempts(file);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("3.json")));

            Assert.True(storage.LockFile(file));
            Assert.NotNull(file);
            file.Modified = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("3.json.x")));
            storage.ReleaseOldLocks();
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("3.json")));

            batch = storage.GetEventBatch(serializer);
            Assert.Equal(1, batch.Count);
            Assert.True(storage.GetFileList().All(f => f.Path.StartsWith("q\\") && f.Path.EndsWith("4.json.x")));
            storage.DeleteBatch(batch);
            Assert.Equal(0, storage.GetQueueFiles().Count());

            ev = new Event { Type = Event.KnownTypes.Log, Message = "test" };
            storage.Enqueue(ev, serializer);
            file = storage.GetFileList().FirstOrDefault();
            Assert.NotNull(file);
            file.Created = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            storage.DeleteOldQueueFiles();
            Assert.Equal(0, storage.GetQueueFiles().Count());
        }
    }
}
