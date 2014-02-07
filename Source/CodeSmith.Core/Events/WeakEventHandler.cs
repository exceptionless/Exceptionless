using System;
using System.ComponentModel;

namespace CodeSmith.Core.Events {

    /// <summary>
    /// Delegate of an unsubscribe delegate
    /// </summary>
    public delegate void UnregisterDelegate<H>(H eventHandler) where H : class;

    /// <summary>
    /// A handler for an event that doesn't store a reference to the source
    /// handler must be a instance method
    /// </summary>
    /// <typeparam name="T">type of calling object</typeparam>
    /// <typeparam name="E">type of event args</typeparam>
    /// <typeparam name="H">type of event handler</typeparam>
    public class WeakEventHandlerGeneric<T, E, H>
        where T : class
        where E : EventArgs
        where H : class {

        private delegate void OpenEventHandler(T @this, object sender, E e);

        private delegate void LocalHandler(object sender, E e);

        private WeakReference _targetRef;
        private OpenEventHandler _openHandler;
        private H _handler;
        private UnregisterDelegate<H> _unregister;

        public WeakEventHandlerGeneric(H eventHandler, UnregisterDelegate<H> unregister) {
            _targetRef = new WeakReference((eventHandler as Delegate).Target);
            _openHandler = (OpenEventHandler)Delegate.CreateDelegate(typeof(OpenEventHandler), null, (eventHandler as Delegate).Method);
            _handler = CastDelegate(new LocalHandler(Invoke));
            _unregister = unregister;
        }

        private void Invoke(object sender, E e) {
            T target = (T)_targetRef.Target;

            if (target != null)
                _openHandler.Invoke(target, sender, e);
            else if (_unregister != null) {
                _unregister(_handler);
                _unregister = null;
            }
        }

        /// <summary>
        /// Gets the handler.
        /// </summary>
        public H Handler {
            get { return _handler; }
        }

        /// <summary>
        /// Performs an implicit conversion from <see cref="WeakEventHandler&lt;T,E&gt;"/> to <see cref="System.EventHandler&lt;E&gt;"/>.
        /// </summary>
        /// <param name="weh">The weh.</param>
        /// <returns>The result of the conversion.</returns>
        public static implicit operator H(WeakEventHandlerGeneric<T, E, H> weh) {
            return weh.Handler;
        }

        /// <summary>
        /// Casts the delegate.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static H CastDelegate(Delegate source) {
            if (source == null)
                return null;

            Delegate[] delegates = source.GetInvocationList();
            if (delegates.Length == 1)
                return Delegate.CreateDelegate(typeof(H), delegates[0].Target, delegates[0].Method) as H;

            for (int i = 0; i < delegates.Length; i++)
                delegates[i] = Delegate.CreateDelegate(typeof(H), delegates[i].Target, delegates[i].Method);

            return Delegate.Combine(delegates) as H;
        }
    }

    #region Weak Generic EventHandler<Args> handler

    /// <summary>
    /// An interface for a weak event handler
    /// </summary>
    /// <typeparam name="E"></typeparam>
    public interface IWeakEventHandler<E> where E : EventArgs {
        EventHandler<E> Handler { get; }
    }

    /// <summary>
    /// A handler for an event that doesn't store a reference to the source
    /// handler must be a instance method
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="E"></typeparam>
    public class WeakEventHandler<T, E> : WeakEventHandlerGeneric<T, E, EventHandler<E>>, IWeakEventHandler<E>
        where T : class
        where E : EventArgs {

        public WeakEventHandler(EventHandler<E> eventHandler, UnregisterDelegate<EventHandler<E>> unregister)
            : base(eventHandler, unregister) { }
    }

    #endregion

    #region Weak PropertyChangedEvent handler

    /// <summary>
    /// An interface for a weak event handler
    /// </summary>
    public interface IWeakPropertyChangedEventHandler {
        PropertyChangedEventHandler Handler { get; }
    }

    /// <summary>
    /// A handler for an event that doesn't store a reference to the source
    /// handler must be a instance method
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class WeakPropertyChangeHandler<T> : WeakEventHandlerGeneric<T, PropertyChangedEventArgs, PropertyChangedEventHandler>, IWeakPropertyChangedEventHandler
     where T : class {

        public WeakPropertyChangeHandler(PropertyChangedEventHandler eventHandler, UnregisterDelegate<PropertyChangedEventHandler> unregister)
            : base(eventHandler, unregister) { }
    }

    #endregion

    /// <summary>
    /// Utilities for the weak event method
    /// </summary>
    public static class WeakEventExtensions {

        public static void CheckArgs(Delegate eventHandler, Delegate unregister) {
            if (eventHandler == null)
                throw new ArgumentNullException("eventHandler");
            if (eventHandler.Method.IsStatic || eventHandler.Target == null)
                throw new ArgumentException("Only instance methods are supported.", "eventHandler");
        }

        public static object GetWeakHandler(Type generalType, Type[] genericTypes, Type[] constructorArgTypes, object[] constructorArgs) {
            var wehType = generalType.MakeGenericType(genericTypes);
            var wehConstructor = wehType.GetConstructor(constructorArgTypes);
            return wehConstructor.Invoke(constructorArgs);
        }

        /// <summary>
        /// Makes a property change handler weak
        /// </summary>
        /// <param name="eventHandler">The event handler.</param>
        /// <param name="unregister">The unregister.</param>
        /// <returns></returns>
        public static PropertyChangedEventHandler MakeWeak(this PropertyChangedEventHandler eventHandler, UnregisterDelegate<PropertyChangedEventHandler> unregister) {
            CheckArgs(eventHandler, unregister);

            var generalType = typeof(WeakPropertyChangeHandler<>);
            var genericTypes = new[] { eventHandler.Method.DeclaringType };
            var constructorTypes = new[] { typeof(PropertyChangedEventHandler), typeof(UnregisterDelegate<PropertyChangedEventHandler>) };
            var constructorArgs = new object[] { eventHandler, unregister };

            return ((IWeakPropertyChangedEventHandler)GetWeakHandler(generalType, genericTypes, constructorTypes, constructorArgs)).Handler;
        }

        /// <summary>
        /// Makes a generic handler weak
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="eventHandler">The event handler.</param>
        /// <param name="unregister">The unregister.</param>
        /// <returns></returns>
        public static EventHandler<E> MakeWeak<E>(this EventHandler<E> eventHandler, UnregisterDelegate<EventHandler<E>> unregister) where E : EventArgs {
            CheckArgs(eventHandler, unregister);

            var generalType = typeof(WeakEventHandler<,>);
            var genericTypes = new[] { eventHandler.Method.DeclaringType, typeof(E) };
            var constructorTypes = new[] { typeof(EventHandler<E>), typeof(UnregisterDelegate<EventHandler<E>>) };
            var constructorArgs = new object[] { eventHandler, unregister };

            return ((IWeakEventHandler<E>)GetWeakHandler(generalType, genericTypes, constructorTypes, constructorArgs)).Handler;
        }
    }
}