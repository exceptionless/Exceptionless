using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class LdapConnectionString : DefaultConnectionString {
        public const string ProviderName = "ldap";

        public LdapConnectionString(string connectionString) : base(connectionString) { }
    }
}