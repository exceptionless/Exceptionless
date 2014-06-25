#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Logging;
using Xunit;

namespace Exceptionless.Client.Tests.Log {
    public class FileExceptionlessLogTests : IDisposable {
        protected const string LOG_FILE = "test.log";

        [Fact]
        public void CanWriteToLogFile() {
            DeleteLog();

            FileExceptionlessLog log = GetLog(LOG_FILE);
            log.Info("Test");
            log.Flush();

            Assert.True(LogExists());
            string contents = GetLogContent();

            Assert.Equal("Test\r\n", contents);

            log.Dispose();
        }

        [Fact]
        public void LogFlushTimerWorks() {
            DeleteLog();

            FileExceptionlessLog log = GetLog(LOG_FILE);
            log.Info("Test");

            string contents = GetLogContent();
            Assert.Equal("", contents);

            Thread.Sleep(1010 * 3);

            Assert.True(LogExists());
            contents = GetLogContent();

            Assert.Equal("Test\r\n", contents);

            log.Dispose();
        }

        [Fact]
        public void LogResetsAfter5mb() {
            DeleteLog();

            FileExceptionlessLog log = GetLog(LOG_FILE);

            // write 3mb of content to the log
            for (int i = 0; i < 1024 * 3; i++)
                log.Info(new string('0', 1024));

            log.Flush();
            Assert.True(GetLogSize() > 1024 * 1024 * 3);

            // force a check file size call
            log.CheckFileSize();

            // make sure it didn't clear the log
            Assert.True(GetLogSize() > 1024 * 1024 * 3);

            // write another 3mb of content to the log
            for (int i = 0; i < 1024 * 3; i++)
                log.Info(new string('0', 1024));

            log.Flush();
            // force a check file size call
            log.CheckFileSize();

            // make sure it cleared the log
            long size = GetLogSize();

            // should be 99 lines of text in the file
            Assert.True(size > 1024 * 99);

            log.Dispose();
        }

        [Fact]
        public void CheckSizeDoesNotFailIfLogIsMissing() {
            FileExceptionlessLog log = GetLog(LOG_FILE + ".doesnotexist");
            Assert.DoesNotThrow(log.CheckFileSize);
        }

        [Fact]
        public void LogIsThreadSafe() {
            DeleteLog();

            FileExceptionlessLog log = GetLog(LOG_FILE);

            // write 3mb of content to the log in multiple threads
            Parallel.For(0, 1024 * 3, i => log.Info(new string('0', 1024)));

            log.Flush();
            Assert.True(GetLogSize() > 1024 * 1024 * 3);

            // force a check file size call
            log.CheckFileSize();

            // make sure it didn't clear the log
            Assert.True(GetLogSize() > 1024 * 1024 * 3);

            // write another 3mb of content to the log
            Parallel.For(0, 1024 * 3, i => log.Info(new string('0', 1024)));
            log.Flush();

            long size = GetLogSize();
            Console.WriteLine("File: " + size);

            // do the check size while writing to the log from multiple threads
            Parallel.Invoke(
                            () => Parallel.For(0, 1024 * 3, i => log.Info(new string('0', 1024))),
                () => {
                    Thread.Sleep(10);
                    log.CheckFileSize();
                });

            // should be more than 99 lines of text in the file
            size = GetLogSize();
            Console.WriteLine("File: " + size);
            Assert.True(size > 1024 * 99);

            log.Dispose();
        }

        protected virtual FileExceptionlessLog GetLog(string filePath) {
            return new FileExceptionlessLog(filePath);
        }

        protected virtual bool LogExists(string path = LOG_FILE) {
            return File.Exists(path);
        }

        protected virtual void DeleteLog(string path = LOG_FILE) {
            if (LogExists(path))
                File.Delete(path);
        }

        protected virtual string GetLogContent(string path = LOG_FILE) {
            if (!LogExists(path))
                return String.Empty;

            using (FileStream stream = File.Open(LOG_FILE, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        protected virtual long GetLogSize(string path = LOG_FILE) {
            var f = new FileInfo(LOG_FILE);
            return f.Length;
        }

        public virtual void Dispose() {}
    }
}