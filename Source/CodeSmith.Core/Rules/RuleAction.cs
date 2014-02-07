using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Rules
{
    public abstract class RuleAction<T>
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public abstract void Run(T context);    
    }
}
