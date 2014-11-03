using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CodeSmith.Core.Reflection;

namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class LateBinderTest
    {
        [Test]
        public void GetPropertyValue()
        {
            Tester tester = new Tester();
            string name = LateBinder.GetProperty(tester, "Name").ToString();

            Assert.AreEqual("test", name);
        }
        [Test]
        public void GetPropertyValueBoxed()
        {
            Tester tester = new Tester();
            var id = LateBinder.GetProperty(tester, "Id");

            Assert.AreEqual(888, id);
        }

        [Test]
        public void GetPropertyValueNested()
        {
            Tester tester = new Tester();
            tester.Order = new Order();

            string name = LateBinder.GetProperty(tester, "Order.Id").ToString();

            Assert.AreEqual("123", name);

        }

        [Test]
        public void SetPropertyValue()
        {
            Tester tester = new Tester();
            LateBinder.SetProperty(tester, "Name", "New Name");

            Assert.AreEqual("New Name", tester.Name);
        }

        [Test]
        public void SetPropertyValueBoxed()
        {
            Tester tester = new Tester();
            LateBinder.SetProperty(tester, "Id", 999);

            Assert.AreEqual(999, tester.Id);
        }

        [Test]
        public void SetPropertyValueNested()
        {
            Tester tester = new Tester();
            tester.Order = new Order();

            LateBinder.SetProperty(tester, "Order.Description", "New Description");

            Assert.AreEqual("New Description", tester.Order.Description);

        }
        [Test]
        public void SetPropertyValueNestedObject()
        {
            Tester tester = new Tester();
            tester.Order = new Order();

            LateBinder.SetProperty(tester, "Order.OrderAddress", new OrderAddress { Zip = "55346" });

            Assert.AreEqual("55346", tester.Order.OrderAddress.Zip);
        }

        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void SetPropertyValueNestedNull()
        {
            Tester tester = new Tester();

            LateBinder.SetProperty(tester, "Order.OrderAddress.City", "New Description");

            Assert.AreEqual("New Description", tester.Order.OrderAddress.City);
        }

        [Test]
        public void GetFieldValue()
        {
            Tester tester = new Tester();
            string name = LateBinder.GetField(tester, "_name").ToString();

            Assert.AreEqual("test", name);
        }
        [Test]
        public void GetFieldValueBoxed()
        {
            Tester tester = new Tester();
            var id = LateBinder.GetField(tester, "_id");

            Assert.AreEqual(888, id);
        }

        [Test]
        public void SetFieldValue()
        {
            Tester tester = new Tester();
            LateBinder.SetField(tester, "_name", "New Name");

            Assert.AreEqual("New Name", tester.Name);
        }

        [Test]
        public void SetFieldValueBoxed()
        {
            Tester tester = new Tester();
            LateBinder.SetField(tester, "_id", 999);

            Assert.AreEqual(999, tester.Id);
        }

        class Tester
        {

            public bool IsSet = false;

            private int _id = 888;

            public int Id
            {
                get { return _id; }
                set { _id = value; }
            }

            private string _name = "test";

            public string Name
            {
                get { return _name; }
                set { _name = value; }
            }

            private string _email = "test@email.com";

            public string Email
            {
                get { return _email; }
                set { _email = value; }
            }

            private Order _order;

            public Order Order
            {
                get { return _order; }
                set { _order = value; }
            }
        }


        class Order
        {
            private int _id = 123;

            public int Id
            {
                get { return _id; }
                set { _id = value; }
            }

            private string _description = "this is the description";

            public string Description
            {
                get { return _description; }
                set { _description = value; }
            }

            public OrderAddress OrderAddress { get; set; }
        }

        class OrderAddress
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
        }

    }
}
