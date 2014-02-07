using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CodeSmith.Core.Extensions
{
    public static class PropertyChangeExtensions
    {
        /// <summary>
        /// Sets the property value if the value is different fromt he existing value. 
        /// The PropertyChanging and PropertyChanged events are raised if the value is updated.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="changedHandler">The handler for the PropertyChanged event.</param>
        /// <param name="changingHandler">The handler for the PropertyChanging event.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="oldValueExpression">The old value expression.</param>
        /// <param name="setter">The setter delegate.</param>
        /// <returns>The new value.</returns>
        /// <example>The following is an example of a Name property.
        /// <code>
        /// <![CDATA[
        /// private string _name;
        /// public string Name
        /// {
        ///     get { return _name; }
        ///     set { PropertyChanged.SetValue(PropertyChanging, value, () => Name, v => _name = v); }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public static T SetValue<T>(
            this PropertyChangedEventHandler changedHandler,
            PropertyChangingEventHandler changingHandler,
            T newValue,
            Expression<Func<T>> oldValueExpression,
            Action<T> setter)
        {
            //Retrieve the old value 
            Func<T> getter = oldValueExpression.Compile();
            T oldValue = getter();

            //In case new and old value both are equal to default 
            //values for that type or are same return 
            if ((Equals(oldValue, default(T)) && Equals(newValue, default(T))) || Equals(oldValue, newValue))
                return newValue;

            //Retrieve the property that has changed 
            var body = oldValueExpression.Body as MemberExpression;
            //var propInfo = body.Member as PropertyInfo;
            string propName = body.Member.Name;
            var targetExpression = body.Expression as ConstantExpression;
            object target = targetExpression.Value;

            //Maintaining the temporary copy of event to avoid race condition 
            var changingHandlerLocal = changingHandler;
            //Raise the event before property is changed 
            if (changingHandlerLocal != null)
                changingHandlerLocal(target, new PropertyChangingEventArgs(propName));

            //Update the property value 
            setter(newValue);

            //Maintaining the temporary copy of event to avoid race condition 
            var changedHandlerLocal = changedHandler;
            //Raise the event after property is changed 
            if (changedHandlerLocal != null)
                changedHandlerLocal(target, new PropertyChangedEventArgs(propName));

            return newValue;
        }

        /// <summary>
        /// Sets the property value if the value is different fromt he existing value. 
        /// The PropertyChanging and PropertyChanged events are raised if the value is updated.
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="changedHandler">The handler for the PropertyChanged event.</param>
        /// <param name="changingHandler">The handler for the PropertyChanging event.</param>
        /// <param name="newValue">The new value.</param>
        /// <param name="oldValueExpression">The old value expression.</param>
        /// <param name="setter">The setter delegate.</param>
        /// <returns>The new value.</returns>
        public static T SetValue<T>(
            this PropertyChangingEventHandler changingHandler,
            PropertyChangedEventHandler changedHandler,
            T newValue,
            Expression<Func<T>> oldValueExpression,
            Action<T> setter)
        {
            return SetValue(changedHandler, changingHandler, newValue, oldValueExpression, setter);
        }

    }
}
