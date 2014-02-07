using System;
using System.Collections.Generic;
using System.Text;

namespace CodeSmith.Core.Helpers
{
    public static class DebugHelper
    {
        private const string DEBUG_IDENTIFIER = "#CST#";


        /// <summary>
        /// Writes the object to the Debug console, prefixed with an identifier for debugView filtering
        /// </summary>
        /// <param name="o">Object to write</param>
        public static void Log(object o)
        { 
            System.Diagnostics.Debug.Write(String.Concat(DEBUG_IDENTIFIER,' ', o.ToString()));
        }

        /// <summary>
        /// Writes the object on a new line to the Debug console, prefixed with an identifier for debugView filtering.
        /// </summary>
        /// <param name="o">Object to write</param>
        public static void LogLine(object o)
        {
            System.Diagnostics.Debug.WriteLine(String.Concat(DEBUG_IDENTIFIER, ' ', o.ToString()));
        }
    }
}
