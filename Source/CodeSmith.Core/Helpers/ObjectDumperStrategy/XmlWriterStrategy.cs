using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;


namespace CodeSmith.Core.Helpers.ObjectDumperStrategy
{
    internal class XmlWriterStrategy : DumperWriterStrategyBase
    {
        private readonly XmlWriter _writer;
        private readonly List<string> _exclusions;

        public XmlWriterStrategy(XmlWriter writer) : this(0, new List<string>(), writer) { }

        public XmlWriterStrategy(int depth, IEnumerable<string> exclusions, XmlWriter writer)
        {
            Depth = depth;
            _writer = writer;
            _exclusions = GetListOfExclusions(exclusions);
        }

        public override void Write(object o)
        {
            if (null == o || o is ValueType || o is string)
            {
                WriteValue(o);
            }
            else if (o is IEnumerable)
            {
                var iterator = o as IEnumerable;
                WriteEnumerable(iterator);
            }
            else
            {
                WriteObjectStart(o);
                WriteMembers(o);
                WriteObjectEnd();
            }
        }

        #region Utility methods
        private bool IsEnumeratorEmpty(object o)
        {
            if (o == null)
                return true;

            if (!(o is IEnumerable))
                return false;

            var iterator = o as IEnumerable;
            IEnumerator enumerator = iterator.GetEnumerator();
            return !(enumerator.MoveNext());
        }

        private bool IsExcludedMember(string name)
        {
            return _exclusions.Any(s => s.Contains(name));
        }

        private List<string> GetListOfExclusions(IEnumerable<string> items)
        {
            var result = new List<string>();

            if (null == items)
                return result;

            result.AddRange(items);

            return result;
        }

        private void ProcessFields(object root, IEnumerable<FieldInfo> fields)
        {
            foreach (FieldInfo field in fields)
                WriteField(root, field);
        }

        private void ProcessProperties(object root, IEnumerable<PropertyInfo> properties)
        {
            foreach (PropertyInfo property in properties)
                WriteProperty(root, property);
        }

        private void WriteField(object root, MemberInfo member)
        {
            var field = member as FieldInfo;
            if (field == null)
                return;
            Type type = field.FieldType;
            Type enumerable = type.GetInterface(typeof(IEnumerable).FullName, false);

            object fieldValue = field.GetValue(root);

            if (null == fieldValue)
                return;

            if (type.IsValueType || type == typeof(string))
            {
                WriteStringOrValueTypeField(field.Name, type.Name, fieldValue);
            }
            else
            {
                Level++;

                if (Level <= Depth)
                    WriteFieldRecursive(field.Name, (null != enumerable) ? enumerable.Name : type.Name, fieldValue);
            }
        }

        private void WriteMembers(object root)
        {
            MemberInfo[] members = root.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance);
            var fields = new List<FieldInfo>();
            var properties = new List<PropertyInfo>();
            int fieldPropertyCount = 0;

            foreach (MemberInfo member in members)
            {
                if (member.MemberType == MemberTypes.Field)
                {
                    if (!IsExcludedMember(member.Name))
                    {
                        fields.Add(member as FieldInfo);
                        fieldPropertyCount++;
                    }
                }

                if (member.MemberType == MemberTypes.Property)
                {
                    if (!IsExcludedMember(member.Name))
                    {
                        properties.Add(member as PropertyInfo);
                        fieldPropertyCount++;
                    }
                }
            }

            if (_exclusions.Count == fieldPropertyCount)
                return;

            ProcessFields(root, fields);
            ProcessProperties(root, properties);
        }

        private void WriteProperty(object root, MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property == null)
                return;

            Type type = property.PropertyType;
            Type enumerable = type.GetInterface(typeof(IEnumerable).FullName, false);

            if (property.GetIndexParameters().Length > 0)
                return;

            object propertyValue = property.GetValue(root, null);

            if (type.IsValueType || type == typeof(string))
            {
                WriteStringOrValueTypeProperty(property.Name, type.Name, propertyValue);
            }
            else
            {
                if (propertyValue == null)
                    return;

                Level++;
                if (Level <= Depth)
                    WritePropertyRecursive(property.Name, (null != enumerable) ? enumerable.Name : type.Name, propertyValue);
            }
        }

        private void WriteString(string s)
        {
            _writer.WriteString(s);
        }

        private void WriteEnumerable(IEnumerable root)
        {
            if (IsEnumeratorEmpty(root))
                return;

            _writer.WriteStartElement("items");
            foreach (object element in root)
            {
                if (element == null)
                    continue;
                
                if (element is ValueType || element is string)
                {
                    _writer.WriteStartElement("item");

                    if (element is DictionaryEntry) {
                        var entry = (DictionaryEntry)element;
                        _writer.WriteAttributeString("key", entry.Key.ToString());
                        WriteValue(entry.Value);
                    }
                    else
                    {
                        _writer.WriteString(element.ToString());
                    }

                    _writer.WriteEndElement();
                }
                else
                {
                    Write(element);
                }
            }
            _writer.WriteEndElement();

        }

        private void WriteObjectStart(object type)
        {
            _writer.WriteStartElement("object", ObjectDumper.XmlNamespace);
            _writer.WriteAttributeString("name", type.GetType().Name);
            _writer.WriteAttributeString("namespace", type.GetType().Namespace);
        }

        private void WriteObjectEnd()
        {
            _writer.WriteEndElement();
        }

        private void WriteStringOrValueTypeField(string name, string typeName, object val)
        {
            if (val == null)
                return;

            _writer.WriteStartElement("f");
            _writer.WriteAttributeString("name", name);
            _writer.WriteAttributeString("type", typeName);
            WriteValue(val);
            _writer.WriteEndElement();
        }

        private void WriteFieldRecursive(string name, string typeName, object val)
        {
            if (val == null || IsEnumeratorEmpty(val))
                return;

            _writer.WriteStartElement("f");
            _writer.WriteAttributeString("name", name);
            _writer.WriteAttributeString("type", typeName);
            Write(val);
            _writer.WriteEndElement();
        }

        private void WriteStringOrValueTypeProperty(string name, string typeName, object val)
        {
            if (val == null)
                return;

            _writer.WriteStartElement("p");
            _writer.WriteAttributeString("name", name);
            _writer.WriteAttributeString("type", typeName);
            WriteValue(val);
            _writer.WriteEndElement();
        }

        private void WritePropertyRecursive(string name, string typeName, object val)
        {
            if (val == null || IsEnumeratorEmpty(val))
                return;

            _writer.WriteStartElement("p");
            _writer.WriteAttributeString("name", name);
            _writer.WriteAttributeString("type", typeName);
            Write(val);
            _writer.WriteEndElement();
        }

        private void WriteValue(object o)
        {
            if (o == null)
                WriteString("null");
            else if (o is DateTime)
                WriteString(((DateTime)o).ToShortDateString());
            else if (o is ValueType || o is string)
                WriteString(o.ToString());
            else if (o is IEnumerable)
                WriteString("...");
            else
                WriteString("{ }");
        }

        #endregion
    }
}
