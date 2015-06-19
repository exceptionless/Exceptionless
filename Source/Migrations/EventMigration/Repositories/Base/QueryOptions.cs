using System;
using System.Collections.Generic;

namespace Exceptionless.EventMigration.Repositories {
    public class QueryOptions {
        public QueryOptions() {
            Ids = new List<string>();
            OrganizationIds = new List<string>();
            ProjectIds = new List<string>();
            StackIds = new List<string>();
        }

        public List<string> Ids { get; set; }
        public List<string> OrganizationIds { get; set; }
        public List<string> ProjectIds { get; set; }
        public List<string> StackIds { get; set; }
    }
}