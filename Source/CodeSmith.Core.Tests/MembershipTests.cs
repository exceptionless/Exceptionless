using System;
using System.Collections.Specialized;
using System.Web.Security;

using NUnit.Framework;

namespace CodeSmith.Core.Tests {
    [TestFixture]
    public class MembershipTests {
        [Test, Ignore("Connects to a SQL Database.")]
        public void GetAllUsers() {
            var mp = new CustomMembershipProvider();
            var config = new NameValueCollection();
            config["connectionStringName"] = "PetShopSQLServer";
            mp.Initialize("MyMembershipProvider", config);
            int totalRecords;
            MembershipUserCollection users = mp.GetAllUsers(0, 10, out totalRecords);
        }
    }
}