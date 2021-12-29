using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MBBSEmu.Converters
{
    /// <summary>
    ///     Handles Conversion of String & Integer values to boolean
    /// </summary>
    public class JsonBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    {
                        var value = reader.GetInt32();

                        if (value is not 0 and not 1)
                            throw new JsonException();

                        return value == 1;
                    }
                case JsonTokenType.String:
                    {
                        var value = reader.GetString();

                        if (value == null)
                            throw new JsonException();

                        return value.ToLower() switch
                        {
                            "1" => true,
                            "true" => true,
                            "yes" => true,
                            "0" => false,
                            "false" => false,
                            "no" => false,
                            _ => throw new JsonException()
                        };
                    }
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteStringValue(value ? "1" : "0");
    }
}
