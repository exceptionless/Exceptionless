using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using CodeSmith.Core.Helpers;
using CodeSmith.Core.Helpers.ObjectDumperStrategy;
using NUnit.Framework;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class TextWriterStrategyTest
    {
        [Test]
        public void CheckNull()
        {
            long expected = 294;
            long one = GetLength(One, 1);
            Assert.AreEqual(expected, one);
        }

        [Test]
        public void CheckOneStreamLength()
        {
            long expected = 195;
            long one = GetLength(One, 0);
            Assert.AreEqual(expected, one);
        }

        [Test]
        public void CheckOneStreamLengthWithDepth()
        {
            long expected = 294;
            long one = GetLength(One, 1);
            Assert.AreEqual(expected, one);
        }

        [Test]
        public void CheckOneStreamLengthWithDepthAndWriter()
        {
            long expected = 294;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    ObjectDumper.Write(One, 1, textWriter);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(result, expected);
        }

        [Test]
        public void CheckTwoStreamLength()
        {
            long expected = 195;
            long two = GetLength(Two, 0);
            Assert.AreEqual(expected, two);
        }

        [Test]
        public void CheckTwoStreamLengthWithDepth()
        {
            long expected = 499;
            long two = GetLength(Two, 1);
            Assert.AreEqual(expected, two);
        }

        [Test]
        public void CheckTwoStreamLengthWithDepthAndWriter()
        {
            long expected = 499;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    ObjectDumper.Write(Two, 1, textWriter);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(result, expected);
        }

        [Test]
        public void TestTextWriterConstructor()
        {
            long expected = 195;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    TextWriterStrategy strategy = new TextWriterStrategy(textWriter);
                    strategy.Write(One);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestTextWriterConstructorWithDepth()
        {
            long expected = 610;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    TextWriterStrategy strategy = new TextWriterStrategy(2,textWriter);
                    strategy.Write(Two);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestTextWriterWithListOfList()
        {
            long expected = 211;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    TextWriterStrategy strategy = new TextWriterStrategy(textWriter);
                    strategy.Write(Three);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestTextWriterWithListOfListWithDepth()
        {
            long expected = 750;
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    TextWriterStrategy strategy = new TextWriterStrategy(2, textWriter);
                    strategy.Write(Three);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            Assert.AreEqual(expected, result);
        }


        private long GetLength(object o, int depth)
        {
            long result = Int64.MinValue;

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter textWriter = new StreamWriter(stream))
                {
                    ObjectDumper.Write(o, depth, textWriter);
                    textWriter.Flush();
                    result = stream.Length;
                }
            }

            return result;
        }

        private DumperStub One
        {
            get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor", null, "Jeff Test Field"); }
        }

        //For equality check
        private DumperStub OneDotOne
        {
            get { return new DumperStub("Jeff Gonzalez", "Jeff's Descriptor", null, "Jeff Test Field"); }
        }

        //For inequality check
        private DumperStub Two
        {
            get { return new DumperStub("Robert Gonzalez", "Robert's Descriptor", One, "Robert Test Field"); }
        }

        //For ListOfList
        private DumperStubEx Three
        {
            get { return new DumperStubEx("Robert Gonzalez", "Robert's Descriptor", One, "Robert Test Field"); }
        }

    }
}
