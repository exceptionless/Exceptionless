// Copyright (C) 2010 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CodeSmith.Core.Reflection
{
    public class ArraySpec
    {
        int dimensions;
        bool bound;

        internal ArraySpec(int dimensions, bool bound)
        {
            this.dimensions = dimensions;
            this.bound = bound;
        }

        public override string ToString()
        {
            if (bound)
                return "[*]";
            string str = "[";
            for (int i = 1; i < dimensions; ++i)
                str += ",";
            return str + "]";
        }
    }

    /// <summary>
    /// A class to parse an assembly-qualified name of a Type into its parts.
    /// </summary>
    public class TypeSpec
    {
        private string name;
        private string assembly_name;
        private List<string> nested;
        private List<TypeSpec> generic_params;
        private List<ArraySpec> array_spec;
        private int pointer_level;
        bool is_byref;

        public bool IsArray()
        {
            return array_spec != null;
        }
        
        public bool IsGenericType()
        {
            return generic_params != null;
        }

        public string GetName()
        {
            if (nested != null && nested.Count > 0)
                return nested.Last();
            if (String.IsNullOrEmpty(name))
                return String.Empty;

            var parts = name
                .Split(Type.Delimiter)
                .Select(s => s.Trim());

            return parts.Last();
        }

        public string GetFullName()
        {
            string str = name;
            if (nested != null)
            {
                foreach (var n in nested)
                    str += "+" + n;
            }

            if (generic_params != null)
            {
                str += "[";
                for (int i = 0; i < generic_params.Count; ++i)
                {
                    if (i > 0)
                        str += ", ";
                    if (generic_params[i].assembly_name != null)
                        str += "[" + generic_params[i] + "]";
                    else
                        str += generic_params[i];
                }
                str += "]";
            }

            if (array_spec != null)
            {
                foreach (var ar in array_spec)
                    str += ar;
            }

            for (int i = 0; i < pointer_level; ++i)
                str += "*";

            if (is_byref)
                str += "&";
            return str;
        }

        public string GetAssemblyName()
        {
            return assembly_name;
        }

        public TypeSpec[] GetGenericParameters()
        {
            return generic_params.ToArray();
        }

        public override string ToString()
        {
            string str = GetFullName();

            if (assembly_name != null)
                str += ", " + assembly_name;

            return str;
        }


        /// <summary>
        /// Parses an assembly-qualified name of a Type into its parts.
        /// </summary>
        /// <param name="typeName">The assembly-qualified name of the Type.</param>
        /// <returns></returns>
        public static TypeSpec Parse(string typeName)
        {
            int pos = 0;
            if (typeName == null)
                throw new ArgumentNullException("typeName");

            TypeSpec res = Parse(typeName, ref pos, false, false);
            if (pos < typeName.Length)
                throw new ArgumentException("Count not parse the whole type name", "typeName");
            return res;
        }

        private void AddName(string type_name)
        {
            if (name == null)
            {
                name = type_name;
            }
            else
            {
                if (nested == null)
                    nested = new List<string>();
                nested.Add(type_name);
            }
        }

        private void AddArray(ArraySpec array)
        {
            if (array_spec == null)
                array_spec = new List<ArraySpec>();
            array_spec.Add(array);
        }

        static void SkipSpace(string name, ref int pos)
        {
            int p = pos;
            while (p < name.Length && Char.IsWhiteSpace(name[p]))
                ++p;
            pos = p;
        }

        static TypeSpec Parse(string name, ref int p, bool is_recurse, bool allow_aqn)
        {
            int pos = p;
            int name_start;
            bool in_modifiers = false;
            TypeSpec data = new TypeSpec();

            SkipSpace(name, ref pos);

            name_start = pos;

            for (; pos < name.Length; ++pos)
            {
                switch (name[pos])
                {
                    case '+':
                        data.AddName(name.Substring(name_start, pos - name_start));
                        name_start = pos + 1;
                        break;
                    case ',':
                    case ']':
                        data.AddName(name.Substring(name_start, pos - name_start));
                        name_start = pos + 1;
                        in_modifiers = true;
                        if (is_recurse && !allow_aqn)
                        {
                            p = pos;
                            return data;
                        }
                        break;
                    case '&':
                    case '*':
                    case '[':
                        if (name[pos] != '[' && is_recurse)
                            throw new ArgumentException("Generic argument can't be byref or pointer type", "typeName");
                        data.AddName(name.Substring(name_start, pos - name_start));
                        name_start = pos + 1;
                        in_modifiers = true;
                        break;
                }
                if (in_modifiers)
                    break;
            }

            if (name_start < pos)
                data.AddName(name.Substring(name_start, pos - name_start));

            if (in_modifiers)
            {
                for (; pos < name.Length; ++pos)
                {

                    switch (name[pos])
                    {
                        case '&':
                            if (data.is_byref)
                                throw new ArgumentException("Can't have a byref of a byref", "typeName");

                            data.is_byref = true;
                            break;
                        case '*':
                            if (data.is_byref)
                                throw new ArgumentException("Can't have a pointer to a byref type", "typeName");
                            ++data.pointer_level;
                            break;
                        case ',':
                            if (is_recurse)
                            {
                                int end = pos;
                                while (end < name.Length && name[end] != ']')
                                    ++end;
                                if (end >= name.Length)
                                    throw new ArgumentException("Unmatched ']' while parsing generic argument assembly name");
                                data.assembly_name = name.Substring(pos + 1, end - pos - 1).Trim();
                                p = end + 1;
                                return data;
                            }
                            data.assembly_name = name.Substring(pos + 1).Trim();
                            pos = name.Length;
                            break;
                        case '[':
                            if (data.is_byref)
                                throw new ArgumentException("Byref qualifier must be the last one of a type", "typeName");
                            ++pos;
                            if (pos >= name.Length)
                                throw new ArgumentException("Invalid array/generic spec", "typeName");
                            SkipSpace(name, ref pos);

                            if (name[pos] != ',' && name[pos] != '*' && name[pos] != ']')
                            {//generic args
                                List<TypeSpec> args = new List<TypeSpec>();
                                if (data.IsArray())
                                    throw new ArgumentException("generic args after array spec", "typeName");

                                while (pos < name.Length)
                                {
                                    SkipSpace(name, ref pos);
                                    bool aqn = name[pos] == '[';
                                    if (aqn)
                                        ++pos; //skip '[' to the start of the type
                                    args.Add(Parse(name, ref pos, true, aqn));
                                    if (pos >= name.Length)
                                        throw new ArgumentException("Invalid generic arguments spec", "typeName");

                                    if (name[pos] == ']')
                                        break;
                                    if (name[pos] == ',')
                                        ++pos; // skip ',' to the start of the next arg
                                    else
                                        throw new ArgumentException("Invalid generic arguments separator " + name[pos], "typeName");

                                }
                                if (pos >= name.Length || name[pos] != ']')
                                    throw new ArgumentException("Error parsing generic params spec", "typeName");
                                data.generic_params = args;
                            }
                            else
                            { //array spec
                                int dimensions = 1;
                                bool bound = false;
                                while (pos < name.Length && name[pos] != ']')
                                {
                                    if (name[pos] == '*')
                                    {
                                        if (bound)
                                            throw new ArgumentException("Array spec cannot have 2 bound dimensions", "typeName");
                                        bound = true;
                                    }
                                    else if (name[pos] != ',')
                                        throw new ArgumentException("Invalid character in array spec " + name[pos], "typeName");
                                    else
                                        ++dimensions;

                                    ++pos;
                                    SkipSpace(name, ref pos);
                                }
                                if (name[pos] != ']')
                                    throw new ArgumentException("Error parsing array spec", "typeName");
                                if (dimensions > 1 && bound)
                                    throw new ArgumentException("Invalid array spec, multi-dimensional array cannot be bound", "typeName");
                                data.AddArray(new ArraySpec(dimensions, bound));
                            }

                            break;
                        case ']':
                            if (is_recurse)
                            {
                                p = pos + 1;
                                return data;
                            }
                            throw new ArgumentException("Unmatched ']'", "typeName");
                        default:
                            throw new ArgumentException("Bad type def, can't handle '" + name[pos] + "'" + " at " + pos, "typeName");
                    }
                }
            }

            p = pos;
            return data;
        }
    }
}
