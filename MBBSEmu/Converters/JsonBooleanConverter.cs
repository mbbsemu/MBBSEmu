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
                    return reader.GetInt32() == 1;
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

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case true:
                    writer.WriteStringValue("1");
                    break;
                case false:
                    writer.WriteStringValue("0");
                    break;
            }
        }
    }
}
