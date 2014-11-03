using System;
using System.IO;
using System.Text;
using System.Xml;
using CodeSmith.Core.Helpers;
using CodeSmith.Core.Helpers.ObjectDumperStrategy;
using NUnit.Framework;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class XmlWriterStrategyTest {
        [Test]
        public void TestOneXmlStreamLength() {
            long one = GetXmlLength(One, 0);
            Assert.AreEqual(370, one);
        }

        [Test]
        public void TestOneXmlStreamLengthWithDepth() {
            long one = GetXmlLength(One, 1);
            Assert.AreEqual(477, one);
        }

        [Test]
        public void TestOneXmlStreamLengthWithDepthAndWriter() {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                ObjectDumper.WriteXml(One, 1, xmlWriter);
                xmlWriter.Flush();
                result = stream.Length;
            }

            Assert.AreEqual(477, result);
        }

        [Test]
        public void TestTwoXmlStreamLength() {
            long two = GetXmlLength(Two, 0);
            Assert.AreEqual(368, two);
        }

        [Test]
        public void TestTwoXmlStreamLengthWithDepth() {
            long two = GetXmlLength(Two, 1);
            Assert.AreEqual(475, two);
        }

        [Test]
        public void TestTwoXmlStreamLengthWithDepthAndWriter() {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                ObjectDumper.WriteXml(Two, 1, xmlWriter);
                xmlWriter.Flush();
                result = stream.Length;
            }

            Assert.AreEqual(475, result);
        }

        [Test]
        public void TestXmlWriterConstructor() {
            var builder = new StringBuilder();
            XmlWriter xmlWriter = XmlWriter.Create(builder);
            var strategy = new XmlWriterStrategy(xmlWriter);
            strategy.Write(One);
            xmlWriter.Flush();
            int result = builder.Length;

            Assert.AreEqual(368, result);
        }

        [Test]
        public void TestXmlWriterConstructorWithDepth() {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                var strategy = new XmlWriterStrategy(1, null, xmlWriter);
                strategy.Write(Two);
                xmlWriter.Flush();
                result = stream.Length;
            }

            Assert.AreEqual(475, result);
        }

        [Test]
        public void TestXmlWriterWithListOfList() {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                var strategy = new XmlWriterStrategy(0, null, xmlWriter);
                strategy.Write(Three);
                xmlWriter.Flush();
                result = stream.Length;
            }

            Assert.AreEqual(370, result);
        }

        [Test]
        public void TestXmlWriterWithListOfListWithDepth() {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                var strategy = new XmlWriterStrategy(2, null, xmlWriter);
                strategy.Write(Three);
                xmlWriter.Flush();
                result = stream.Length;
            }

            Assert.AreEqual(632, result);
        }

        private long GetXmlLength(object o, int depth) {
            long result;

            using (var stream = new MemoryStream()) {
                XmlWriter xmlWriter = XmlWriter.Create(stream);
                ObjectDumper.WriteXml(o, depth, xmlWriter);
                xmlWriter.Flush();
                result = stream.Length;
            }

            return result;
        }

        private DumperStub One {
            get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor <sds", null, "Jeff Test Field"); }
        }

        //For equality check
        private DumperStub OneDotOne {
            get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor", null, "Jeff Test Field"); }
        }

        //For inequality check
        private DumperStub Two {
            get { return new DumperStub("Robert Gonzalez", "Robert's Descriptor", One, "Robert Test Field"); }
        }

        //For ListOfList
        private DumperStubEx Three {
            get { return new DumperStubEx("Robert Gonzalez", "Robert's Descriptor", One, "Robert Test Field"); }
        }
    }
}