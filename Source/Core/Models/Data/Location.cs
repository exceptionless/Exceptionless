using System;
using System.Diagnostics;

namespace Exceptionless.Core.Models.Data {
    [DebuggerDisplay("{Locality}, {Level2}, {Level1}, {Country}")]
    public class Location {
        public string Country { get; set; }

        /// <summary>
        /// State / Province
        /// </summary>
        public string Level1 { get; set; }
        
        /// <summary>
        /// County
        /// </summary>
        public string Level2 { get; set; }
        
        /// <summary>
        /// City
        /// </summary>
        public string Locality { get; set; }
    }
}