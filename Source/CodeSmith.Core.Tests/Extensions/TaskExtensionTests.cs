using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using NUnit.Framework;

namespace CodeSmith.Core.Tests.Extensions
{
    [TestFixture]
    internal class TaskExtensionTests
    {
        [Test]
        public void TryContinueWithTest()
        {
            Assert.DoesNotThrow(() =>
                {
                    var task = Task.Factory.StartNew(() =>
                        {
                            throw new Exception();
                        }).TryContinueWith(t =>
                            Assert.IsTrue(t.IsFaulted)
                        );
                    task.Wait();
                });
        }

        [Test]
        public void TryContinueWithNoExceptionTest()
        {
            var task = Task.Factory.StartNew(() =>
            {
            }).TryContinueWith(t =>
                Assert.IsFalse(t.IsFaulted)
            );

            task.Wait();
        }

        [Test]
        [ExpectedException(typeof(AggregateException))]
        public void ContinueWithTest()
        {
            {
                var task = Task.Factory.StartNew(() =>
                {
            Assert.Catch(() =>
                    throw new Exception();
                }).ContinueWith(t =>
                        Assert.IsTrue(t.IsFaulted)
                    );

                task.Wait();
            });
        }

        [Test]
        public void ContinueWithNoExceptionTest()
        {
            var task = Task.Factory.StartNew(() =>
            {
            }).ContinueWith(t =>
                    Assert.IsFalse(t.IsFaulted)
            );

            task.Wait();
        }
    }
}
