using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Rules
{
    public class RuleContext<T> where T : class
    {
        public bool IsComplete { get; set; }
        public Exception Error { get; set; }

        public T State { get; set; }
    }
}
