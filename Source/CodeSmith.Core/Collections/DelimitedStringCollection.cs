using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.Collections
{
    /// <summary>
    /// A collection that provides notifications when items get added, removed, or when the whole list is refreshed. 
    /// </summary>
#if !SILVERLIGHT
    [Serializable]
    [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        "System.Drawing.Design.UITypeEditor, System.Drawing, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
#endif
    [TypeConverter(typeof(DelimitedStringTypeConverter))]
    public class DelimitedStringCollection : Collection<string>, INotifyPropertyChanged
    {
        private const string CountString = "Count";
        private const string IndexerName = "Item[]";
        private readonly SimpleMonitor _monitor = new SimpleMonitor();

        /// <summary>
        /// Initializes a new instance of the <see cref="DelimitedStringCollection"/> class.
        /// </summary>
        public DelimitedStringCollection()
        {
            Delimiter = '|';
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelimitedStringCollection"/> class.
        /// </summary>
        /// <param name="collection">The collection.</param>
        public DelimitedStringCollection(IEnumerable<string> collection)
            : this()
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            CopyFrom(collection);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelimitedStringCollection"/> class.
        /// </summary>
        /// <param name="delimitedValues">The delimited values.</param>
        public DelimitedStringCollection(string delimitedValues)
            : this()
        {
            CopyFrom(delimitedValues);
        }

        /// <summary>
        /// Gets or sets the string delimiter.
        /// </summary>
        /// <value>The string delimiter.</value>
        [DefaultValue('|')]
        public char Delimiter { get; set; }

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Occurs when a property value changed.
        /// </summary>
#if !SILVERLIGHT
        [field: NonSerialized]
#endif
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        /// <summary>
        /// Occurs when the collection changed.
        /// </summary>
#if !SILVERLIGHT
        [field: NonSerialized]
#endif
        public event EventHandler CollectionChanged;

        /// <summary>
        /// Disallows reentrant attempts to change this collection.
        /// </summary>
        /// <returns>An IDisposable object that can be used to dispose of the object.</returns>
        protected IDisposable BlockReentrancy()
        {
            _monitor.Enter();
            return _monitor;
        }

        /// <summary>
        /// Checks for reentrant attempts to change this collection.
        /// </summary>
        protected void CheckReentrancy()
        {
            if ((_monitor.Busy && (CollectionChanged != null)) && (CollectionChanged.GetInvocationList().Length > 1))
                throw new InvalidOperationException("Observable collection reentrancy not allowed.");
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.ObjectModel.Collection`1"/>.
        /// </summary>
        protected override void ClearItems()
        {
            CheckReentrancy();
            base.ClearItems();
            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
        }

        private void CopyFrom(IEnumerable<string> collection)
        {
            IList<string> items = Items;
            if ((collection == null) || (items == null))
                return;

            using (IEnumerator<string> enumerator = collection.GetEnumerator())
            {
                while (enumerator.MoveNext())
                    items.Add(enumerator.Current);
            }
        }

        /// <summary>
        /// Inserts an element into the <see cref="T:System.Collections.ObjectModel.Collection`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
        /// <param name="item">The object to insert. The value can be null for reference types.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void InsertItem(int index, string item)
        {
            CheckReentrancy();
            base.InsertItem(index, item);
            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
        }

        /// <summary>
        /// Moves the item at the specified index to a new location in the collection.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        public void Move(int oldIndex, int newIndex)
        {
            MoveItem(oldIndex, newIndex);
        }

        /// <summary>
        /// Moves the item at the specified index to a new location in the collection.
        /// </summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            CheckReentrancy();
            string item = base[oldIndex];
            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, item);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
        }

        /// <summary>
        /// Raises the CollectionChanged event with the provided arguments.
        /// </summary>
        protected virtual void OnCollectionChanged()
        {
            if (CollectionChanged != null)
                using (BlockReentrancy())
                {
                    CollectionChanged(this, EventArgs.Empty);
                }
        }

        /// <summary>
        /// Raises the PropertyChanged event with the provided arguments.
        /// </summary>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        /// <summary>
        /// Raises the PropertyChanged event with the provided arguments.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        private void OnPropertyChanged(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="T:System.Collections.ObjectModel.Collection`1"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is equal to or greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void RemoveItem(int index)
        {
            CheckReentrancy();
            //string item = base[index];
            base.RemoveItem(index);
            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
        }

        /// <summary>
        /// Replaces the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to replace.</param>
        /// <param name="item">The new value for the element at the specified index. The value can be null for reference types.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">
        /// 	<paramref name="index"/> is less than zero.
        /// -or-
        /// <paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void SetItem(int index, string item)
        {
            CheckReentrancy();
            //string oldItem = base[index];
            base.SetItem(index, item);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return ToString(Items, Delimiter);
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the values.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="delimiter">The delimiter.</param>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public static string ToString(IEnumerable<string> values, char delimiter)
        {
            StringBuilder buffer = new StringBuilder();
            foreach (string item in values)
            {
                if (buffer.Length > 0)
                    buffer.Append(delimiter);

                bool quote = item.Any(c => c == '"' || c == delimiter);

                if (quote)
                {
                    buffer.Append('"');
                    buffer.Append(item.Replace("\"", "\"\""));
                    buffer.Append('"');
                    continue;
                }

                buffer.Append(item);
            }
            return buffer.ToString();
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the values.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public static string ToString(IEnumerable<string> values)
        {
            return ToString(values, '|');
        }

        /// <summary>
        /// Returns an array of strings that represents the current collection.
        /// </summary>
        /// <returns>An array of strings that represents the current collection.</returns>
        public string[] ToArray()
        {
            var values = new string[Count];
            CopyTo(values, 0);

            return values;
        }

        /// <summary>
        /// Adds the split values from <paramref name="delimitedValues"/> to the collection.
        /// The string is split using the <see cref="Delimiter"/> property.
        /// </summary>
        /// <param name="delimitedValues">The delimited values.</param>
        /// <returns>The number of values added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="delimitedValues"/> is null.</exception>
        public int AddDelimited(string delimitedValues)
        {
            CheckReentrancy();
            int i = CopyFrom(delimitedValues);
            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged();
            return i;
        }

        private int CopyFrom(string delimitedValues)
        {
            if (String.IsNullOrEmpty(delimitedValues))
                return 0;

            int i = Count;
            var values = Parse(delimitedValues, Delimiter);
            CopyFrom(values);
            return Count - i;
        }

        /// <summary>
        /// Parses the specified delimited values.
        /// </summary>
        /// <param name="delimitedValues">The delimited values.</param>
        /// <param name="delimiter">The list delimiters.</param>
        /// <returns></returns>
        public static IEnumerable<string> Parse(string delimitedValues, params char[] delimiter)
        {
            if (delimitedValues.IsNullOrEmpty())
                throw new ArgumentNullException("delimitedValues");
            if (delimiter == null)
                throw new ArgumentNullException("delimiter");
            if (delimiter.Length == 0)
                throw new ArgumentOutOfRangeException("delimiter", "There must be at least on delimiter specified.");

            StringBuilder buffer = new StringBuilder();
            StringReader reader = new StringReader(delimitedValues);

            bool withinQuotes = false;

            do
            {
                char c = (char)reader.Read();

                // whitespace preceeding data, discard
                if (buffer.Length == 0 && c == ' ')
                    continue;

                // look for double quote
                if (withinQuotes && c == '"' && reader.Peek() == '"')
                {
                    buffer.Append(c);
                    // discard double quote
                    reader.Read();
                    continue;
                }

                if (c == '"')
                {
                    withinQuotes = !withinQuotes;
                    continue;
                }

                if (!withinQuotes && delimiter.Any(x => x == c))
                {
                    yield return buffer.ToString();
                    buffer.Length = 0;
                    continue;
                }
                
                buffer.Append(c);
            }
            while (reader.Peek() != -1);

            yield return buffer.ToString();
        }

        #region Nested type: DelimitedStringTypeConverter

        /// <summary>
        /// A type converter for the <see cref="DelimitedStringCollection"/>.
        /// </summary>
        public class DelimitedStringTypeConverter : TypeConverter
        {
            /// <summary>
            /// Returns whether this converter can convert an object of the given type to the type of this converter, using the specified context.
            /// </summary>
            /// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context.</param>
            /// <param name="sourceType">A <see cref="T:System.Type"/> that represents the type you want to convert from.</param>
            /// <returns>
            /// true if this converter can perform the conversion; otherwise, false.
            /// </returns>
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                if (sourceType == typeof(string))
                    return true;

                return base.CanConvertFrom(context, sourceType);
            }

            /// <summary>
            /// Returns whether this converter can convert the object to the specified type, using the specified context.
            /// </summary>
            /// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context.</param>
            /// <param name="destinationType">A <see cref="T:System.Type"/> that represents the type you want to convert to.</param>
            /// <returns>
            /// true if this converter can perform the conversion; otherwise, false.
            /// </returns>
            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                if (destinationType == typeof(string))
                    return true;

                return base.CanConvertTo(context, destinationType);
            }

            /// <summary>
            /// Converts the given object to the type of this converter, using the specified context and culture information.
            /// </summary>
            /// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context.</param>
            /// <param name="culture">The <see cref="T:System.Globalization.CultureInfo"/> to use as the current culture.</param>
            /// <param name="value">The <see cref="T:System.Object"/> to convert.</param>
            /// <returns>
            /// An <see cref="T:System.Object"/> that represents the converted value.
            /// </returns>
            /// <exception cref="T:System.NotSupportedException">
            /// The conversion cannot be performed.
            /// </exception>
            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                if (!(value is string))
                    return base.ConvertFrom(context, culture, value);

#if !SILVERLIGHT
                DelimitedStringCollection list;
                if (context.PropertyDescriptor.IsReadOnly)
                {
                    list = context.PropertyDescriptor.GetValue(context.Instance) as DelimitedStringCollection;
                    if (list != null)
                        list.AddDelimited(value.ToString());
                }
                else
                    list = new DelimitedStringCollection(value.ToString());

                return list ?? base.ConvertFrom(context, culture, value);
#else
                return new DelimitedStringCollection(value.ToString()); ;
#endif
            }

            /// <summary>
            /// Converts the given value object to the specified type, using the specified context and culture information.
            /// </summary>
            /// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context.</param>
            /// <param name="culture">A <see cref="T:System.Globalization.CultureInfo"/>. If null is passed, the current culture is assumed.</param>
            /// <param name="value">The <see cref="T:System.Object"/> to convert.</param>
            /// <param name="destinationType">The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to.</param>
            /// <returns>
            /// An <see cref="T:System.Object"/> that represents the converted value.
            /// </returns>
            /// <exception cref="T:System.ArgumentNullException">
            /// The <paramref name="destinationType"/> parameter is null.
            /// </exception>
            /// <exception cref="T:System.NotSupportedException">
            /// The conversion cannot be performed.
            /// </exception>
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                var list = value as DelimitedStringCollection;

                if (destinationType != typeof(string) || list == null)
                    return base.ConvertTo(context, culture, value, destinationType);

                return list.ToString();
            }
        }

        #endregion

        #region Nested type: SimpleMonitor

#if !SILVERLIGHT
        [Serializable]
#endif
        private class SimpleMonitor : IDisposable
        {
            private int _busyCount;

            public bool Busy
            {
                get { return (_busyCount > 0); }
            }

            #region IDisposable Members

            public void Dispose()
            {
                _busyCount--;
            }

            #endregion

            public void Enter()
            {
                _busyCount++;
            }
        }

        #endregion
    }
}