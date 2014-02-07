//Derived from ObjectDumper - Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace CodeSmith.Core.Helpers.ObjectDumperStrategy
{
    internal class TextWriterStrategy : DumperWriterStrategyBase
    {
        TextWriter writer;
        int pos;
        List<string> exclusions;

        public TextWriterStrategy(TextWriter writer) : this(0, writer) { }

        public TextWriterStrategy(int depth, TextWriter writer) : this(depth, new List<string>(), writer)
        {
        }

        public TextWriterStrategy(int depth, IEnumerable<string> exclusions, TextWriter writer)
        {
            this.writer = writer;
            this.Depth = depth;
            this.exclusions = GetListOfExclusions(exclusions);
        }

        override public void Write(object o)
        {
            Write(null, o);
        }

        private void Write(object prefix, object o)
        {
            string s = prefix as string;
            WriteObject(s, o);
        }

        private void WriteString(string s)
        {
            if (s != null)
            {
                writer.Write(s);
                pos += s.Length;
            }
        }

        private void WriteIndent()
        {
            for (int i = 0; i < Level; i++) writer.Write("  ");
        }

        private void WriteLine()
        {
            writer.WriteLine();
            pos = 0;
        }

        private void WriteTab()
        {
            WriteString("  ");
            while (pos % 8 != 0) WriteString(" ");
        }

        private void WriteObject(string prefix, object o)
        {
            if (o == null || o is ValueType || o is string)
            {
                WriteIndent();
                WriteString(prefix);
                WriteValue(o);
                WriteLine();
            }
            else if (o is IEnumerable)
            {
                foreach (object element in (IEnumerable)o)
                {
                    if (element is IEnumerable && !(element is string))
                    {
                        WriteIndent();
                        WriteString(prefix);
                        WriteString("...");
                        WriteLine();
                        if (Level < Depth)
                        {
                            Level++;
                            WriteObject(prefix, element);
                            Level--;
                        }
                    }
                    else
                    {
                        WriteObject(prefix, element);
                    }
                }
            }
            else
            {
                MemberInfo[] members = o.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance);
                WriteIndent();
                WriteString(prefix);
                bool propWritten = false;
                foreach (MemberInfo m in members)
                {
                    FieldInfo f = m as FieldInfo;
                    PropertyInfo p = m as PropertyInfo;
                    if (f != null || p != null)
                    {
                        bool isExcluded = false; 

                        if (null != f && null == p)
                        {
                            isExcluded = IsExcludedMember(f.Name);
                        }

                        if (null != p && null == f)
                        {
                            isExcluded = IsExcludedMember(p.Name);
                        }

                        if (!isExcluded)
                        {
                            if (propWritten)
                            {
                                WriteTab();
                            }
                            else
                            {
                                propWritten = true;
                            }
                            WriteString(m.Name);
                            WriteString("=");
                            Type t = f != null ? f.FieldType : p.PropertyType;
                            if (t.IsValueType || t == typeof(string))
                            {
                                WriteValue(f != null ? f.GetValue(o) : p.GetValue(o, null));
                            }
                            else
                            {
                                if (typeof(IEnumerable).IsAssignableFrom(t))
                                {
                                    WriteString("...");
                                }
                                else
                                {
                                    WriteString("[ ]");
                                }
                            }
                        }
                    }
                }
                if (propWritten) WriteLine();
                if (Level < Depth)
                {
                    foreach (MemberInfo m in members)
                    {
                        FieldInfo f = m as FieldInfo;
                        PropertyInfo p = m as PropertyInfo;
                        if (f != null || p != null)
                        {
                            Type t = f != null ? f.FieldType : p.PropertyType;
                            if (!(t.IsValueType || t == typeof(string)))
                            {
                                object value = f != null ? f.GetValue(o) : p.GetValue(o, null);
                                if (value != null)
                                {
                                    Level++;
                                    WriteObject(m.Name + ": ", value);
                                    Level--;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void WriteValue(object o)
        {
            if (o == null)
            {
                WriteString("null");
            }
            else if (o is DateTime)
            {
                WriteString(((DateTime)o).ToShortDateString());
            }
            else if (o is ValueType || o is string)
            {
                WriteString(o.ToString());
            }
            else if (o is IEnumerable)
            {
                WriteString("...");
            }
            else
            {
                WriteString("{ }");
            }
        }

        private List<string> GetListOfExclusions(IEnumerable<string> items)
        {
            List<string> result = new List<string>();

            if (null == items)
                return result;

            foreach (string s in items)
                result.Add(s);

            return result;
        }

        private bool IsExcludedMember(string name)
        {
            foreach (string s in exclusions)
            {
                if (s.Contains(name))
                    return true;
            }

            return false;
        }
    }
}
