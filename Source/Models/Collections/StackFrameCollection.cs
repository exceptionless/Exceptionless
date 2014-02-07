#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.ObjectModel;
using System.Text;

namespace Exceptionless.Models {
    public class StackFrameCollection : Collection<StackFrame> {
        public override string ToString() {
            var sb = new StringBuilder(255);
            AppendStackFrames(sb);
            return sb.ToString();
        }

        internal void AppendStackFrames(StringBuilder sb, bool appendNewLine = false, string methodPrefix = "at ", bool linkFilePath = false, string traceIndentValue = "   ") {
            bool first = true;
            foreach (StackFrame frame in this) {
                if (String.IsNullOrEmpty(frame.Name))
                    continue;

                if (first)
                    first = false;
                else
                    sb.Append(Environment.NewLine);

                frame.AppendStackFrame(sb, methodPrefix, linkFilePath: linkFilePath, traceIndentValue: traceIndentValue);
            }

            if (appendNewLine)
                sb.Append(Environment.NewLine);
        }
    }
}