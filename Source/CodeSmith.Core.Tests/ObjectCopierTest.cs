using System;
using System.Collections.Generic;
using System.Text;
using CodeSmith.Core;
using CodeSmith.Core.Reflection;
using NUnit.Framework;


namespace CodeSmith.Core.Tests
{
    [TestFixture]
    public class ObjectCopierTest
    {
        [Test]
        public void CopyTest()
        {
            CopyBase s = new CopyBase();
            FillSource(s);
            CopyDestination d = new CopyDestination();

            ObjectCopier.Copy(s, d);

            Assert.AreEqual(s.Id, d.Id);
            Assert.AreEqual(s.Email, d.Email);
            Assert.AreEqual(s.FirstName, d.FirstName);
            Assert.AreEqual(s.LastName, d.LastName);
            Assert.AreEqual(s.Phone, d.Phone);

        }

        [Test]
        public void CopyExtra()
        {
            CopyBase s = new CopyBase();
            FillSource(s);
            CopyDestinationExtra d = new CopyDestinationExtra();

            ObjectCopier.Copy(s, d);

            Assert.AreEqual(s.Id, d.Id);
            Assert.AreEqual(s.Email, d.Email);
            Assert.AreEqual(s.FirstName, d.FirstName);
            Assert.AreEqual(s.LastName, d.LastName);
            Assert.AreEqual(s.Phone, d.Phone);
            Assert.IsNull(d.Address);
        }

        [Test]
        public void CopyExtraReverse()
        {
            CopyDestinationExtra s = new CopyDestinationExtra();
            FillSource(s);
            CopyBase d = new CopyBase();

            ObjectCopier.Copy(s, d);

            Assert.AreEqual(s.Id, d.Id);
            Assert.AreEqual(s.Email, d.Email);
            Assert.AreEqual(s.FirstName, d.FirstName);
            Assert.AreEqual(s.LastName, d.LastName);
            Assert.AreEqual(s.Phone, d.Phone);
        }

        [Test]
        public void CopyTypeChange()
        {
            CopyDestinationExtra s = new CopyDestinationExtra();
            FillSource(s);
            s.Address = "123 Street";
            s.City = "Any City";
            s.State = "NY";
            s.Zip = "12345";

            CopyDestinationType d = new CopyDestinationType();

            ObjectCopier.Copy(s, d);

            Assert.AreEqual(s.Id, d.Id);
            Assert.AreEqual(s.Email, d.Email);
            Assert.AreEqual(s.FirstName, d.FirstName);
            Assert.AreEqual(s.LastName, d.LastName);
            Assert.AreEqual(s.Phone, d.Phone);
        }

        [Test]
        public void CopyToDictionary()
        {
            CopyBase s = new CopyBase();
            FillSource(s);
            Dictionary<string, object> d = new Dictionary<string, object>();

            ObjectCopier.Copy(s, d);

            Assert.Greater(d.Count, 0);

            Assert.AreEqual(s.Id, d["Id"]);
            Assert.AreEqual(s.Email, d["Email"]);
            Assert.AreEqual(s.FirstName, d["FirstName"]);
            Assert.AreEqual(s.LastName, d["LastName"]);
            Assert.AreEqual(s.Phone, d["Phone"]);
        }

        [Test]
        public void CopyFromDictionary()
        {
            Dictionary<string, object> s = new Dictionary<string, object>();
            s.Add("Id", 1);
            s.Add("Email", "user@email");
            s.Add("FirstName", "test");
            s.Add("LastName", "user");
            s.Add("Phone", "800-555-1212");

            CopyBase d = new CopyBase();

            ObjectCopier.Copy(s, d);

            Assert.AreEqual(s["Id"], d.Id);
            Assert.AreEqual(s["Email"], d.Email);
            Assert.AreEqual(s["FirstName"], d.FirstName);
            Assert.AreEqual(s["LastName"], d.LastName);
            Assert.AreEqual(s["Phone"], d.Phone);

        }

        private void FillSource(CopyBase c)
        {
            c.Id = 1;
            c.Email = "user@email";
            c.FirstName = "test";
            c.LastName = "user";
            c.Phone = "800-555-1212";
        }

        private class CopyBase
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
        }

        private class CopyDestination : CopyBase
        {
        }

        private class CopyDestinationExtra : CopyBase
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
        }

        private class CopyDestinationType : CopyBase
        {
            public string Address { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public int Zip { get; set; }
        }
    }




}
