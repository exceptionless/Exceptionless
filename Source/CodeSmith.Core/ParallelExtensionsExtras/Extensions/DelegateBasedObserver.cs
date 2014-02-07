//--------------------------------------------------------------------------
// 
//  Copyright (c) Microsoft Corporation.  All rights reserved. 
// 
//  File: IProducerConsumerCollectionExtensions.cs
//
//--------------------------------------------------------------------------

#if !SILVERLIGHT && !PFX_LEGACY_3_5

using System;

namespace System
{
    internal class DelegateBasedObserver<T> : IObserver<T>
    {
        private Action<T> _onNext;
        private Action<Exception> _onError;
        private Action _onCompleted;

        internal DelegateBasedObserver(Action<T> onNext, Action<Exception> onError, Action onCompleted)
        {
            if (onNext == null) throw new ArgumentNullException("onNext");
            if (onError == null) throw new ArgumentNullException("onError");
            if (onCompleted == null) throw new ArgumentNullException("onCompleted");
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnCompleted() { _onCompleted(); }
        public void OnError(Exception error) { _onError(error); }
        public void OnNext(T value) { _onNext(value); }
    }
}

#endif