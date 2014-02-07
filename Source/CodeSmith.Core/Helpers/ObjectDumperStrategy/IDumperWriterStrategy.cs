using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSmith.Core.Helpers.ObjectDumperStrategy
{
    public interface IDumperWriterStrategy
    {
        void Write(Object o);
    }
}
