using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodeSmith.Core.IO {
    [Serializable]
    public sealed class NullTextWriter : TextWriter {
        public NullTextWriter() : base(CultureInfo.InvariantCulture) {}

        public override void Write(string value) {}

        public override void Write(char[] buffer, int index, int count) {}

        public override void WriteLine() {}

        public override void WriteLine(object value) {}

        public override void WriteLine(string value) {}

        public override Encoding Encoding {
            get { return Encoding.Default; }
        }
    }
}