using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeSmith.Core.Helpers;
using NUnit.Framework;
using System.Web;
using System.Xml;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class ObjectDumperTest {
        [Test]
        public void TestWriteWithSingleObject() {
            var consoleOut = Console.Out;
            
            using (var writer = new StringWriter()) {
                Console.SetOut(writer);
                ObjectDumper.Write(One);
                Assert.AreEqual(195, writer.ToString().Length);
            }

            Console.SetOut(consoleOut);
        }

        [Test]
        public void TestWriteWithSingleObjectWithDepth() {
            var consoleOut = Console.Out;

            using (var writer = new StringWriter()) {
                Console.SetOut(writer);
                ObjectDumper.Write(One, 1);
                Assert.AreEqual(294, writer.ToString().Length);
            }

            Console.SetOut(consoleOut);
        }

        [Test]
        public void TestWriteXmlWithSingleObject() {
            var consoleOut = Console.Out;

            using (var writer = new StringWriter()) {
                Console.SetOut(writer);
                ObjectDumper.WriteXml(One);
                Assert.AreEqual(376, writer.ToString().Length);
            }

            Console.SetOut(consoleOut);
        }

        [Test]
        public void TestWriteXmlWithSingleObjectWithDepth() {
            var consoleOut = Console.Out;

            using (var writer = new StringWriter()) {
                Console.SetOut(writer);
                ObjectDumper.WriteXml(One, 1);
                Assert.AreEqual(512, writer.ToString().Length);
            }

            Console.SetOut(consoleOut);
        }

        [Test]
        public void TestWriteXmlException() {
            var ex = new HttpUnhandledException("This is a test");

            var buffer = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(buffer))
                ObjectDumper.WriteXml(ex, 1, new[] { "Message" }, writer);

            Assert.AreEqual(296, buffer.ToString().Length);

        }

        [Test]
        public void ExceptionWithNestedCustomData() {
            var buffer = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(buffer))
                ObjectDumper.WriteXml(GetExceptionWithData(), 1, writer);

            var xml = buffer.ToString();

            Assert.IsNotNull(xml);
            Assert.IsFalse(xml.Contains("System.Collections.DictionaryEntry"));
            Assert.IsTrue(xml.Contains("{ }"));
        }

        private Exception GetExceptionWithData() {
            var exception = new InvalidOperationException("OPPSIES!!!! Something Jacked UP!!");
            exception.Data["KeyThatShouldBeShown"] = "Some data that should be shown in CodeSmith";
            exception.Data["KeyThatShouldBeShown2"] = "Some data that should be shown in CodeSmith 2";
            exception.Data["KeyThatShouldBeShown3"] = "Some data that should be shown in CodeSmith 3";
            exception.Data["KeyThatShouldBeShown4"] = new TestExceptionData();

            return exception;
        }

        private DumperStub One { get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor", null, "Jeff Test Field"); } }

        //For equality check
        private DumperStub OneDotOne { get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor", null, "Jeff Test Field"); } }

        //For inequality check
        private DumperStub Two { get { return new DumperStub("Robert Gonzalez", "Robert's Descriptor", One, "Robert Test Field"); } }
    }

    [Serializable]
    public class TestExceptionData {
        private readonly string _someData;
        private readonly string _someData2;
        private readonly string _someData3;
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        public TestExceptionData() {
            _someData = "Some Additional Data 1";
            _someData2 = "Some Additional Data 2";
            _someData3 = "Some Additional Data 3";
            _dictionary.Add("Test Dictionary", "Test Data");
        }

        public override string ToString() {
            return string.Format("someData: {0}, someData2: {1}, someData3: {2}", _someData, _someData2, _someData3);
        }
    }
}
