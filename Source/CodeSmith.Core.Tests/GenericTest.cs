using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Diagnostics;
using NUnit.Framework;

namespace GenericTest
{
    [TestFixture]
    public class GenericTest
    {
        [Test]
        public void CreateGenericAtRuntime()
        {
            Stopwatch watch = Stopwatch.StartNew();
            // define the generic class at runtime
            Type genericType = typeof(Collection<>).MakeGenericType(typeof(DateTime));
            // create an instance of the generic type
            object instance = Activator.CreateInstance(genericType);
            watch.Stop();

            Console.WriteLine("Dynamic Create Time: {0} ms", watch.Elapsed.TotalMilliseconds);

            Assert.IsNotNull(instance, "instance is Null");
            Assert.IsTrue(instance is Collection<DateTime>);

            Collection<DateTime> dates = instance as Collection<DateTime>;

            Assert.IsNotNull(dates, "dates is Null");

            watch = Stopwatch.StartNew();
            Collection<DateTime> d = new Collection<DateTime>();
            watch.Stop();

            Console.WriteLine("Normal Create Time: {0} ms", watch.Elapsed.TotalMilliseconds);
        }

        public Collection<T> GetCollection<T>()
        {
            return new Collection<T>();
        }

        [Test]
        public void CallGenericMethodAtRuntime()
        {
            Stopwatch watch = Stopwatch.StartNew();
            MethodInfo methodInfo = typeof(GenericTest).GetMethod("GetCollection");
            MethodInfo genericInfo = methodInfo.MakeGenericMethod(typeof(DateTime));

            object instance = genericInfo.Invoke(this, null);
            watch.Stop();

            Console.WriteLine("Dynamic Invoke Time: {0} ms", watch.Elapsed.TotalMilliseconds);

            Assert.IsNotNull(instance, "instance is Null");
            Assert.IsTrue(instance is Collection<DateTime>);

            Collection<DateTime> dates = instance as Collection<DateTime>;

            Assert.IsNotNull(dates, "dates is Null");

            watch = Stopwatch.StartNew();
            Collection<DateTime> d = this.GetCollection<DateTime>();
            watch.Stop();

            Console.WriteLine("Normal Invoke Time: {0} ms", watch.Elapsed.TotalMilliseconds);
        }
    }
}
