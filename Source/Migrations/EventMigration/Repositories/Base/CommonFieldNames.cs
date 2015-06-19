using System;

namespace Exceptionless.EventMigration.Repositories {
    public static class CommonFieldNames {
        public const string Id = "_id";
        public const string ProjectId = "pid";
        public const string StackId = "sid";
        public const string OrganizationId = "oid";
        public const string Date = "dt";
        public const string Date_UTC = "dt.0";
        public const string Data = "ext";
    }
}