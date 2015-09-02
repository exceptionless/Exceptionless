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