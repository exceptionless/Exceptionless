using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Rules
{
    /// <summary>
    /// The rule operator for criteria.
    /// </summary>
    public enum RuleOperator
    {
        /// <summary>
        /// A comparison for equality.  
        /// </summary>
        Equal,
        /// <summary>
        /// A comparison for greater than.  
        /// </summary>
        GreaterThan,
        /// <summary>
        /// A comparison for greater than or equal to. 
        /// </summary>
        GreaterThanOrEqual,
        /// <summary>
        /// A comparison for less than.  
        /// </summary>
        LessThan,
        /// <summary>
        /// A comparison for less than or equal to.  
        /// </summary>
        LessThanOrEqual,
        /// <summary>
        /// A comparison for inequality.  
        /// </summary>
        NotEqual,
        /// <summary>
        /// A comparison for partial match
        /// </summary>
        Contains,

        StartsWith,

        EndsWith,

        NotContains,

        Regex
    }

    public class RuleCriteria
    {
        public string Field { get; set; }
        public RuleOperator Operator { get; set; }
        public string Value { get; set; }
    }
}
