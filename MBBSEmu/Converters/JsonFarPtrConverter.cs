using MBBSEmu.Memory;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MBBSEmu.Converters
{
    public class JsonFarPtrConverter : JsonConverter<FarPtr>
    {
        public override FarPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                {
                    var value = reader.GetString();

                    if (value == null)
                        throw new JsonException();

                    return new FarPtr(value);
                }

                default:
                    throw new ArgumentException($"Invalid JsonToken Type: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, FarPtr value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }
}
