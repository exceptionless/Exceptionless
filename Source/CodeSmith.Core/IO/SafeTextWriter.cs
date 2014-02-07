using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodeSmith.Core.IO
{
    [Serializable]
    public class SafeTextWriter : TextWriter {
        private readonly TextWriter _writer;

        public SafeTextWriter(TextWriter writer) : base(CultureInfo.InvariantCulture) {
            _writer = writer;
        }

        public override void Write(string value) {
            try {
                if (_writer != null)
                    _writer.Write(value);
            } catch (Exception ex) {
                LogError(ex);
            }
        }

        public override void Write(char[] buffer, int index, int count) {
            try {
                if (_writer != null)
                    _writer.Write(buffer, index, count);
            } catch (Exception ex) {
                LogError(ex);
            }
        }

        public override void WriteLine() {
            try {
                if (_writer != null)
                    _writer.WriteLine();
            } catch (Exception ex) {
                LogError(ex);
            }
        }

        public override void WriteLine(object value) {
            try {
                if (_writer != null)
                    _writer.WriteLine(value);
            } catch (Exception ex) {
                LogError(ex);
            }
        }

        public override void WriteLine(string value) {
            try {
                if (_writer != null)
                    _writer.WriteLine(value);
            } catch (Exception ex) {
                LogError(ex);
            }
        }

        public override Encoding Encoding
        {
            get { return _writer != null ? _writer.Encoding : Encoding.Default; }
        }

        protected virtual void LogError(Exception ex) {}
    }
}