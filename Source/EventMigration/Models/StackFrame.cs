#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class StackFrame : Method {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }

        public Exceptionless.Core.Models.Data.StackFrame ToStackFrame() {
            var frame = new Exceptionless.Core.Models.Data.StackFrame {
                Name = Name,
                ModuleId = ModuleId,
                DeclaringNamespace = DeclaringNamespace,
                DeclaringType = DeclaringType,
                IsSignatureTarget = IsSignatureTarget,
                Column = Column,
                FileName = FileName,
                LineNumber = LineNumber
            };

            if (GenericArguments != null && GenericArguments.Count > 0)
                frame.GenericArguments = GenericArguments.ToGenericArguments();

            if (Parameters != null && Parameters.Count > 0)
                frame.Parameters = Parameters.ToParameters();

            if (ExtendedData != null && ExtendedData.Count > 0)
                frame.Data.AddRange(ExtendedData.ToData());

            return frame;
        }
    }
}