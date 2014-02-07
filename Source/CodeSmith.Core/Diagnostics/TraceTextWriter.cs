//------------------------------------------------------------------------------
//
// Copyright (c) 2002-2014 CodeSmith Tools, LLC.  All rights reserved.
// 
// The terms of use for this software are contained in the file
// named sourcelicense.txt, which can be found in the root of this distribution.
// By using this software in any fashion, you are agreeing to be bound by the
// terms of this license.
// 
// You must not remove this notice, or any other, from this software.
//
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace CodeSmith.Core.Diagnostics
{
    public class TraceTextWriter : TextWriter
    {
        private string _category = String.Empty;
        private Encoding _encoding = null;

        public TraceTextWriter()
        {
        }

        public TraceTextWriter(string category)
        {
            _category = category;
        }

        public override Encoding Encoding
        {
            get
            {
                if (_encoding == null)
                {
                    _encoding = new UnicodeEncoding(false, false);
                }

                return _encoding;
            }
        }

        public override void Write(char value)
        {
            if (String.IsNullOrEmpty(_category))
            {
                Trace.Write(value);
            }
            else
            {
                Trace.Write(value, _category);
            }
        }

        public override void Write(string value)
        {
            if (String.IsNullOrEmpty(_category))
            {
                Trace.Write(value);
            }
            else
            {
                Trace.Write(value, _category);
            }
        }
    }
}
