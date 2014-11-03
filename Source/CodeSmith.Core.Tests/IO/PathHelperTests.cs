using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodeSmith.Core.IO;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.IO
{
    [TestFixture]
    public class PathHelperTests
    {
        [SetUp]
        public void Setup()
        {
            //TODO: NUnit setup
        }

        [TearDown]
        public void TearDown()
        {
            //TODO: NUnit TearDown
        }

        [Test]
        public void GetUniqueName()
        {
            string p = PathHelper.GetUniqueName(@"IO\Document.txt");
            Assert.AreEqual(@"IO\Document[1].txt", @"IO\Document[1].txt");

            p = PathHelper.GetUniqueName(@"IO\Document - Copy.txt");
            Assert.AreEqual(@"IO\Document - Copy[3].txt", @"IO\Document - Copy[3].txt");
        }

        [Test]
        public void GetCleanName()
        {
            string p = PathHelper.GetCleanPath(@"IO\Document.txt");
            Assert.AreEqual(@"IO\Document.txt", p);

            p = PathHelper.GetCleanPath(@"IO\<Document>.txt");
            Assert.AreEqual(@"IO\Document.txt", p);

            p = PathHelper.GetCleanPath(@"IO\Doc|ument.txt");
            Assert.AreEqual(@"IO\Document.txt", p);

            p = PathHelper.GetCleanPath(@"IO\Doc|ument.txt");
            Assert.AreEqual(@"IO\Document.txt", p);

        }

        [Test]
        public void Combine()
        {
            string path = PathHelper.Combine("p1", "p2", "p3");
            Assert.AreEqual(@"p1\p2\p3", path);

            path = PathHelper.Combine("p1");
            Assert.AreEqual(@"p1", path);

            path = PathHelper.Combine("p1", null, "p3");
            Assert.AreEqual(@"p1\p3", path);

            path = PathHelper.Combine("p1", string.Empty, "p3");
            Assert.AreEqual(@"p1\p3", path);

            path = PathHelper.Combine(@"c:\p1", @"p2\p4", "p3");
            Assert.AreEqual(@"c:\p1\p2\p4\p3", path);

            path = PathHelper.Combine(@"c:\p1", 123, Guid.Empty);
            Assert.AreEqual(@"c:\p1\123\00000000-0000-0000-0000-000000000000", path);


        }

        [Test]
        public void DataMacroWithSeperator()
        {
            string expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.xml");
            string path = @"|DataDirectory|\data.xml";
            string r = PathHelper.ExpandPath(path);

            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void DataMacroWithoutSeperator()
        {
            string expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.xml");
            string path = @"|DataDirectory|data.xml";
            string r = PathHelper.ExpandPath(path);
            
            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void DataMacroOnly()
        {
            string expected = AppDomain.CurrentDomain.BaseDirectory;
            string path = @"|DataDirectory|";
            string r = PathHelper.ExpandPath(path);
            
            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void DataMacroOnlySeperator()
        {
            string expected = AppDomain.CurrentDomain.BaseDirectory;
            string path = @"|DataDirectory|\";
            string r = PathHelper.ExpandPath(path);
            
            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void NoDataMacro()
        {
            string expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.xml");
            const string path = @"data.xml";
            string r = PathHelper.ExpandPath(path);

            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void RelativePathToDifferentRoots()
        {
            const string expected = "A:\\Templates\\Frameworks\\PLINQO\\CSharp\\Entities.cst";
            const string root = "G:\\Documents\\ConsoleApplication";
            string r = PathHelper.RelativePathTo(root, expected);

            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void RelativePathToSubFolder()
        {
            const string expected = "Common\\";
            const string fromPath = "G:\\Documents\\ConsoleApplication";
            const string toPath = "G:\\Documents\\ConsoleApplication\\Common\\";
            string r = PathHelper.RelativePathTo(fromPath, toPath);

            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }

        [Test]
        public void RelativePathToParent()
        {
            const string expected = @"..";
            const string fromPath = "G:\\Documents\\ConsoleApplication\\Common\\";
            const string toPath = "G:\\Documents\\ConsoleApplication";
            string r = PathHelper.RelativePathTo(fromPath, toPath);

            Console.WriteLine(r);
            Assert.AreEqual(expected, r);
        }
    }
}
