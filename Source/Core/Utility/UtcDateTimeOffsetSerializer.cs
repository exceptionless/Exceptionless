using System;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Options;

namespace MongoDB.Bson.Serialization.Serializers {
    public class UtcDateTimeOffsetSerializer : BsonBaseSerializer {
        public UtcDateTimeOffsetSerializer() : base(new RepresentationSerializationOptions(BsonType.Array)) {
        }

        public override object Deserialize(
            BsonReader bsonReader,
            Type nominalType,
            Type actualType,
            IBsonSerializationOptions options) {
            VerifyTypes(nominalType, actualType, typeof(DateTimeOffset));

            BsonType bsonType = bsonReader.GetCurrentBsonType();
            long ticks;
            TimeSpan offset;
            switch (bsonType) {
            case BsonType.Array:
                bsonReader.ReadStartArray();
                ticks = bsonReader.ReadInt64();
                offset = TimeSpan.FromMinutes(bsonReader.ReadInt32());
                bsonReader.ReadEndArray();
                return new DateTimeOffset(ticks, offset).Add(offset);
            case BsonType.Document:
                bsonReader.ReadStartDocument();
                bsonReader.ReadDateTime("DateTime");
                ticks = bsonReader.ReadInt64("Ticks");
                offset = TimeSpan.FromMinutes(bsonReader.ReadInt32("Offset"));
                bsonReader.ReadEndDocument();
                return new DateTimeOffset(ticks, offset).Add(offset);
            default:
                var message = string.Format("Cannot deserialize DateTimeOffset from BsonType {0}.", bsonType);
                throw new ArgumentException(message);
            }
        }

        public override void Serialize(
            BsonWriter bsonWriter,
            Type nominalType,
            object value,
            IBsonSerializationOptions options) {
            var dateTimeOffset = (DateTimeOffset)value;
            var representationSerializationOptions = EnsureSerializationOptions<RepresentationSerializationOptions>(options);

            switch (representationSerializationOptions.Representation) {
            case BsonType.Array:
                bsonWriter.WriteStartArray();
                bsonWriter.WriteInt64(dateTimeOffset.UtcTicks);
                bsonWriter.WriteInt32((int)dateTimeOffset.Offset.TotalMinutes);
                bsonWriter.WriteEndArray();
                break;
            case BsonType.Document:
                bsonWriter.WriteStartDocument();
                bsonWriter.WriteDateTime("DateTime", BsonUtils.ToMillisecondsSinceEpoch(dateTimeOffset.UtcDateTime));
                bsonWriter.WriteInt64("Ticks", dateTimeOffset.UtcTicks);
                bsonWriter.WriteInt32("Offset", (int)dateTimeOffset.Offset.TotalMinutes);
                bsonWriter.WriteEndDocument();
                break;
            default:
                var message = string.Format("'{0}' is not a valid DateTimeOffset representation.", representationSerializationOptions.Representation);
                throw new BsonSerializationException(message);
            }
        }
    }
}