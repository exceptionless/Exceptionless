using System;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace MongoMigrations {
    public class MigrationVersionSerializer : BsonBaseSerializer {
        public override void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options) {
            var version = (MigrationVersion)value;
            var versionString = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Revision);
            bsonWriter.WriteString(versionString);
        }

        public override object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options) {
            var versionString = bsonReader.ReadString();
            return new MigrationVersion(versionString);
        }
    }
}