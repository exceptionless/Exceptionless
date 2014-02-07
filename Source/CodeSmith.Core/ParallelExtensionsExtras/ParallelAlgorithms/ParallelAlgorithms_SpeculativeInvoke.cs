//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: ParallelAlgorithms_SpeculativeInvoke.cs
//
//--------------------------------------------------------------------------

#if !SILVERLIGHT && !PFX_LEGACY_3_5

using System.Collections.Concurrent.Partitioners;
using System.Threading.Tasks;

namespace System.Threading.Algorithms
{
    public static partial class ParallelAlgorithms
    {
        /// <summary>Invokes the specified functions, potentially in parallel, canceling outstanding invocations once one completes.</summary>
        /// <typeparam name="T">Specifies the type of data returned by the functions.</typeparam>
        /// <param name="functions">The functions to be executed.</param>
        /// <returns>A result from executing one of the functions.</returns>
        public static T SpeculativeInvoke<T>(params Func<T>[] functions)
        {
            // Run with default options
            return SpeculativeInvoke(s_defaultParallelOptions, functions);
        }

        /// <summary>Invokes the specified functions, potentially in parallel, canceling outstanding invocations once one completes.</summary>
        /// <typeparam name="T">Specifies the type of data returned by the functions.</typeparam>
        /// <param name="options">The options to use for the execution.</param>
        /// <param name="functions">The functions to be executed.</param>
        /// <returns>A result from executing one of the functions.</returns>
        public static T SpeculativeInvoke<T>(ParallelOptions options, params Func<T>[] functions)
        {
            // Validate parameters
            if (options == null) throw new ArgumentNullException("options");
            if (functions == null) throw new ArgumentNullException("functions");

            // Speculatively invoke each function
            return ParallelAlgorithms.SpeculativeForEach(functions, options, function => function());
        }
    }
}

#endif