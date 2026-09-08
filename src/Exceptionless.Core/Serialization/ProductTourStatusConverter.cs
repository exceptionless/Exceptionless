using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Serialization;

public sealed class ProductTourStatusConverter : JsonConverter<ProductTourStatus>
{
    private static readonly JsonConverter<ProductTourStatus> Reader =
        (JsonConverter<ProductTourStatus>)new JsonStringEnumConverter<ProductTourStatus>().CreateConverter(typeof(ProductTourStatus), JsonSerializerOptions.Default);

    public override ProductTourStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Reader.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, ProductTourStatus value, JsonSerializerOptions options)
        => writer.WriteNumberValue((int)value);
}
