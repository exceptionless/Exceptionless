#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Exceptionless.Utility;

namespace Exceptionless.Logging {
    public class FileExceptionlessLog : IExceptionlessLog, IDisposable {
        private Timer _flushTimer;
        private readonly bool _append;
        private bool _firstWrite = true;

        public FileExceptionlessLog(string filePath, bool append = false) {
            if (String.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath");

            if (!Path.IsPathRooted(filePath))
                filePath = Path.GetFullPath(filePath);

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            FilePath = filePath;
            _append = append;

            // flush the log every 2 seconds instead of on every write
            _flushTimer = new Timer(OnFlushTimer, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        protected virtual StreamWriter GetWriter(bool append = false) {
            return new StreamWriter(FilePath, append, Encoding.ASCII);
        }

        protected virtual FileStream GetReader() {
            return new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        protected virtual long GetFileSize() {
            try {
                if (File.Exists(FilePath))
                    return new FileInfo(FilePath).Length;
            } catch (IOException ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting size of file: {0}", ex.Message);
            }

            return -1;
        }

        public string FilePath { get; private set; }

        public void Error(string message, string source = null, Exception exception = null) {
            if (source != null)
                WriteLine(String.Concat(source, ": ", message));
            else
                WriteLine(message);

            if (exception != null)
                WriteLine(exception.ToString());
        }

        public void Info(string message, string source = null) {
            if (source != null)
                WriteLine(String.Concat(source, ": ", message));
            else
                WriteLine(message);
        }

        public void Debug(string message, string source = null) {
            if (source != null)
                WriteLine(String.Concat(source, ": ", message));
            else
                WriteLine(message);
        }

        public void Warn(string message, string source = null) {
            if (source != null)
                WriteLine(String.Concat(source, ": ", message));
            else
                WriteLine(message);
        }

        public void Trace(string message, string source = null) {
            if (source != null)
                WriteLine(String.Concat(source, ": ", message));
            else
                WriteLine(message);
        }

        public void Flush() {
            if (_buffer.Count == 0)
                return;

            if (DateTime.Now.Subtract(_lastSizeCheck).TotalSeconds > 120)
                CheckFileSize();

            try {
                Run.WithRetries(() => {
                    using (new SingleGlobalInstance(FilePath.GetHashCode().ToString(), 500)) {
                        bool append = _append || !_firstWrite;
                        _firstWrite = false;

                        try {
                            using (StreamWriter writer = GetWriter(append)) {
                                while (_buffer.Count > 0)
                                    writer.WriteLine(_buffer.Dequeue());
                            }
                        } catch (Exception ex) {
                            System.Diagnostics.Trace.TraceError("Unable flush the logs. " + ex.Message);
                            while (_buffer.Count > 0)
                                System.Diagnostics.Trace.WriteLine(_buffer.Dequeue());
                        }
                    }
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error flushing log contents to disk: {0}", ex.Message);
            }
        }

        private readonly Queue<string> _buffer = new Queue<string>(1000);

        private void WriteLine(string entry) {
            try {
                Run.WithRetries(() => {
                    using (new SingleGlobalInstance(FilePath.GetHashCode().ToString(), 500))
                        _buffer.Enqueue(entry);
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error enqueuing log message: {0}", ex.Message);
            }
        }

        private DateTime _lastSizeCheck = DateTime.Now;
        protected const long FIVE_MB = 5 * 1024 * 1024;

        internal void CheckFileSize() {
            _lastSizeCheck = DateTime.Now;

            if (GetFileSize() <= FIVE_MB)
                return;

            // get the last X lines from the current file
            string lastLines = String.Empty;
            try {
                Run.WithRetries(() => {
                    using (new SingleGlobalInstance(FilePath.GetHashCode().ToString(), 500)) {
                        try {
                            lastLines = GetLastLinesFromFile(FilePath);
                        } catch {}
                    }
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error getting last X lines from the log file: {0}", ex.Message);
            }

            if (String.IsNullOrEmpty(lastLines))
                return;

            // overwrite the log file and initialize it with the last X lines it had
            try {
                Run.WithRetries(() => {
                    using (new SingleGlobalInstance(FilePath.GetHashCode().ToString(), 500)) {
                        using (StreamWriter writer = GetWriter(true))
                            writer.Write(lastLines);
                    }
                });
            } catch (Exception ex) {
                System.Diagnostics.Trace.WriteLine("Exceptionless: Error rewriting the log file after trimming it: {0}", ex.Message);
            }
        }

        private void OnFlushTimer(object state) {
            Flush();
        }

        public virtual void Dispose() {
            if (_flushTimer != null) {
                _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _flushTimer.Dispose();
                _flushTimer = null;
            }

            Flush();
        }

        protected string GetLastLinesFromFile(string path, int lines = 100) {
            byte[] buffer = Encoding.ASCII.GetBytes("\n");

            using (FileStream fs = GetReader()) {
                long lineCount = 0;
                long endPosition = fs.Length;

                for (long position = 1; position < endPosition; position++) {
                    fs.Seek(-position, SeekOrigin.End);
                    fs.Read(buffer, 0, 1);

                    if (buffer[0] != '\n')
                        continue;

                    lineCount++;
                    if (lineCount != lines)
                        continue;

                    var returnBuffer = new byte[fs.Length - fs.Position];
                    fs.Read(returnBuffer, 0, returnBuffer.Length);

                    return Encoding.ASCII.GetString(returnBuffer);
                }

                // handle case where number of lines in file is less than desired line count
                fs.Seek(0, SeekOrigin.Begin);
                buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);

                return Encoding.ASCII.GetString(buffer);
            }
        }
    }
}