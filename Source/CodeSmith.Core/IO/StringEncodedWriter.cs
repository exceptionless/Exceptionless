using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CodeSmith.Core.IO
{
    /// <summary>
    /// Implements a TextWriter with Encoding for writing information to a String. The information is stored in an underlying StringBuilder. 
    /// </summary>
    public class StringEncodedWriter : StringWriter
    {
        private readonly Encoding _encoding;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        public StringEncodedWriter()
            : this(Encoding.UTF8, new StringBuilder(), CultureInfo.CurrentCulture)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that controls formatting.</param>
        public StringEncodedWriter(IFormatProvider formatProvider)
            : this(Encoding.UTF8, new StringBuilder(), formatProvider)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="sb">The StringBuilder to write to.</param>
        public StringEncodedWriter(StringBuilder sb)
            : this(Encoding.UTF8, sb, CultureInfo.CurrentCulture)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="sb">The StringBuilder to write to.</param>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that controls formatting.</param>
        /// <exception cref="T:System.ArgumentNullException">
        /// 	<paramref name="sb"/> is null.
        /// </exception>
        public StringEncodedWriter(StringBuilder sb, IFormatProvider formatProvider)
            : this(Encoding.UTF8, sb, formatProvider)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="encoding">The <see cref="T:System.Text.Encoding"/> in which the output is written.</param>
        public StringEncodedWriter(Encoding encoding)
            : this(encoding, new StringBuilder(), CultureInfo.CurrentCulture)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="encoding">The <see cref="T:System.Text.Encoding"/> in which the output is written.</param>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that controls formatting.</param>
        public StringEncodedWriter(Encoding encoding, IFormatProvider formatProvider)
            : this(encoding, new StringBuilder(), formatProvider)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="encoding">The <see cref="T:System.Text.Encoding"/> in which the output is written.</param>
        /// <param name="sb">The StringBuilder to write to.</param>
        public StringEncodedWriter(Encoding encoding, StringBuilder sb)
            : this(encoding, sb, CultureInfo.CurrentCulture)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncodedWriter"/> class.
        /// </summary>
        /// <param name="encoding">The <see cref="T:System.Text.Encoding"/> in which the output is written.</param>
        /// <param name="sb">The StringBuilder to write to.</param>
        /// <param name="formatProvider">An <see cref="T:System.IFormatProvider"/> object that controls formatting.</param>
        public StringEncodedWriter(Encoding encoding, StringBuilder sb, IFormatProvider formatProvider)
            : base(sb, formatProvider)
        {
            _encoding = encoding;
        }

        /// <summary>
        /// Gets the <see cref="T:System.Text.Encoding"/> in which the output is written.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// The Encoding in which the output is written.
        /// </returns>
        public override Encoding Encoding
        {
            get { return _encoding; }
        }
    }

}
