using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSmith.Core.Helpers.ObjectDumperStrategy
{
    public abstract class DumperWriterStrategyBase : IDumperWriterStrategy
    {
        public abstract void Write(Object o);

        private int _depth;
        protected int Depth
        {
            get { return _depth; }
            set { _depth = value; }
        }

        private int _level;
        protected int Level
        {
            get { return _level; }
            set { _level = value; }
        }
    }
}
