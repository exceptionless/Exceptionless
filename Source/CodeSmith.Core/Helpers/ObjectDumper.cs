// Original ObjectDumper Copyright (c) Microsoft Corporation.  All rights reserved.
// Modified to add XmlWriting
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using CodeSmith.Core.Helpers.ObjectDumperStrategy;

namespace CodeSmith.Core.Helpers
{
    public class ObjectDumper
    {
        public const string XmlNamespace = "http://schemas.exceptionless.com/objectdata";

        /// <summary>
        /// Writes the object's public fields and properties to Console.Out.
        /// </summary>
        /// <param name="o">The object that will be dumped.</param>
        public static void Write(object o)
        {
            Write(o, 0, new string[] { });
        }

        /// <summary>
        /// Writes the object's public fields and properties to Console.Out, excludes fields/properties found in exclusions.
        /// </summary>
        /// <param name="o">The object that will be dumped.</param>
        /// <param name="exclusions">List of excluded fields/properties.</param>
        public static void Write(object o, IEnumerable<string> exclusions)
        {
            Write(o, 0, exclusions);
        }

        public static void Write(object o, int depth)
        {
            Write(o, depth, Console.Out, new string[] {});
        }

        public static void Write(object o, int depth, IEnumerable<string> exclusions)
        {
            Write(o, depth, Console.Out, exclusions);
        }

        public static void Write(object o, int depth, params string[] exclusions)
        {
            Write(o, depth, Console.Out, exclusions);
        }

        public static void Write(object o, int depth, TextWriter log)
        {
            Write(o, depth, log, new string[] { });
        }

        public static void Write(object o, int depth, TextWriter log, params string[] exclusions)
        {
            IDumperWriterStrategy strategy = new TextWriterStrategy(depth, exclusions, log);
            strategy.Write(o);
        }

        public static void Write(object o, int depth, TextWriter log, IEnumerable<string> exclusions)
        {
            IDumperWriterStrategy strategy = new TextWriterStrategy(depth, exclusions, log);
            strategy.Write(o);
        }

        public static void WriteXml(object o)
        {
            WriteXml(o, 0, new string[] { });
        }

        public static void WriteXml(object o, IEnumerable<string> exclusions)
        {
            WriteXml(o, 0, exclusions);
        }

        public static void WriteXml(object o, int depth)
        {
            WriteXml(o, depth, new string[] { });
        }

        public static void WriteXml(object o, int depth, IEnumerable<string> exclusions)
        {
            XmlWriterSettings settings = new XmlWriterSettings() { Indent = true, IndentChars = "\t", Encoding = Encoding.UTF8 };
            XmlWriter writer = XmlWriter.Create(Console.Out, settings);
            WriteXml(o, depth, exclusions, writer);
        }

        public static void WriteXml(object o, int depth, XmlWriter log)
        {
            WriteXml(o, depth, new string[] { }, log);
        }

        public static void WriteXml(object o, int depth, XmlWriter log, params string[] exclusions)
        {
            IDumperWriterStrategy strategy = new XmlWriterStrategy(depth, exclusions, log);
            strategy.Write(o);
        }

        public static void WriteXml(object o, int depth, IEnumerable<string> exclusions, XmlWriter log)
        {
            IDumperWriterStrategy strategy = new XmlWriterStrategy(depth, exclusions, log);
            strategy.Write(o);
            log.Flush();
        }
    }

}
