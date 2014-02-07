using System;
using System.Diagnostics;
using System.IO;
using System.Security.Permissions;

namespace CodeSmith.Core.Diagnostics
{
    /// <summary>
    /// Writes tracing or debugging output to a text file.
    /// </summary>
    [HostProtection(SecurityAction.LinkDemand, Synchronization = true)]
    public class RollingFileTraceListener : TraceListener
    {
        private readonly string _fileName;
        private DateTime _currentDate;
        private StreamWriter _traceWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="RollingFileTraceListener"/> class.
        /// </summary>
        public RollingFileTraceListener()
            : this(GetDefaultPath(), "RollingFileTraceListener")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollingFileTraceListener"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        public RollingFileTraceListener(string fileName)
            : this(fileName, "RollingFileTraceListener")
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollingFileTraceListener"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="name">The name.</param>
        public RollingFileTraceListener(string fileName, string name)
            : base(name)
        {
            _fileName = fileName;
            _traceWriter = new StreamWriter(GenerateFilename(), true);
        }

        /// <summary>
        /// When overridden in a derived class, flushes the output buffer.
        /// </summary>
        public override void Flush()
        {
            if (_traceWriter != null)
                _traceWriter.Flush();
        }

        /// <summary>
        /// Writes the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        public override void Write(string value)
        {
            CheckRollover();
            _traceWriter.Write(value);
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="value">The value.</param>
        public override void WriteLine(string value)
        {
            CheckRollover();
            _traceWriter.WriteLine(value);
        }

        private static string GetDefaultPath()
        {
            object dataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory");

            string defaultPath = (dataDirectory == null) ? String.Empty : dataDirectory.ToString();
            defaultPath = Path.Combine(defaultPath, "Logs");

            if (!Directory.Exists(defaultPath))
                Directory.CreateDirectory(defaultPath);

            defaultPath = Path.Combine(defaultPath, "log.txt");
            defaultPath = Path.GetFullPath(defaultPath);

            return defaultPath;
        }

        private string GenerateFilename()
        {
            _currentDate = DateTime.Today;

            string directoryName = Path.GetDirectoryName(_fileName);
            string fileName = Path.GetFileNameWithoutExtension(_fileName);
            string extension = Path.GetExtension(_fileName);

            string newFileName = String.Format("{0}_{1}{2}",
                                               fileName,
                                               _currentDate.ToString("yyyymmdd"),
                                               extension);

            return Path.Combine(directoryName, newFileName);
        }

        private void CheckRollover()
        {
            if (_traceWriter == null)
                _traceWriter = new StreamWriter(GenerateFilename(), true);

            if (_currentDate.CompareTo(DateTime.Today) == 0)
                return;

            Close();            
            _traceWriter = new StreamWriter(GenerateFilename(), true);
        }

        /// <summary>
        /// When overridden in a derived class, closes the output stream so it no longer receives tracing or debugging output.
        /// </summary>
        public override void Close()
        {
            if (_traceWriter == null)
                return;

            _traceWriter.Close();
            _traceWriter = null;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.Diagnostics.TraceListener"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Close();
        }
    }
}