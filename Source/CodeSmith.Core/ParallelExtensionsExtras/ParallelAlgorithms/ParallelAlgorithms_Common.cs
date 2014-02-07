//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: ParallelAlgorithms_Common.cs
//
//--------------------------------------------------------------------------

#if !SILVERLIGHT && !PFX_LEGACY_3_5

using System.Threading.Tasks;

namespace System.Threading.Algorithms
{
    /// <summary>
    /// Provides parallelized algorithms for common operations.
    /// </summary>
    public static partial class ParallelAlgorithms
    {
        // Default, shared instance of the ParallelOptions class.  This should not be modified.
        private static ParallelOptions s_defaultParallelOptions = new ParallelOptions();
    }
}

#endif