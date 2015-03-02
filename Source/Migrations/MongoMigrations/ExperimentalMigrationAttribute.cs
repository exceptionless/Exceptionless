using System;

namespace MongoMigrations {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ExperimentalAttribute : Attribute { }
}