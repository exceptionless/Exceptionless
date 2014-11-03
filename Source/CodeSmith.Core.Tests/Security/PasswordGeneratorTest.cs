using System;
using CodeSmith.Core.Security;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Security
{
    [TestFixture]
    public class PasswordGeneratorTest
    {
        [Test]
        public void GeneratePassword()
        {
            var generator = new PasswordGenerator();
            string password = generator.Next();
            Console.WriteLine(password);

            Assert.That(password.Length == generator.Length);
        }
    }
}
