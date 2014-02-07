//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: CancellationTokenExtensions.cs
//
//--------------------------------------------------------------------------

#if !SILVERLIGHT && !PFX_LEGACY_3_5

using System.Collections.Concurrent.Partitioners;
using System.Collections.Generic;

namespace System.Threading
{
    /// <summary>Extension methods for CancellationToken.</summary>
    public static class CancellationTokenExtensions
    {
        /// <summary>Cancels a CancellationTokenSource and throws a corresponding OperationCanceledException.</summary>
        /// <param name="source">The source to be canceled.</param>
        public static void CancelAndThrow(this CancellationTokenSource source)
        {
            source.Cancel();
            source.Token.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Creates a CancellationTokenSource that will be canceled when the specified token has cancellation requested.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>The created CancellationTokenSource.</returns>
        public static CancellationTokenSource CreateLinkedSource(this CancellationToken token)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(token, new CancellationToken());
        }
    }
}

#endif