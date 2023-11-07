using MBBSEmu.Module;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MBBSEmu.Converters
{
    /// <summary>
    ///     Custom JSON Converter used to load Module Configuration Files and propagate overriding values (Path, etc.) to
    ///     each Module defined in the configuration file.
    /// </summary>
    public class JsonModuleConfigurationFileConverter : JsonConverter<ModuleConfigurationFile>
    {
        public override ModuleConfigurationFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected a JSON object.");
            }

            var config = new ModuleConfigurationFile();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    // Ensure that the modules are initialized even if not present in the JSON.
                    config.Modules ??= new List<ModuleConfiguration>();
                    return config;
                }

                // Get the property name.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected a property name.");
                }

                var propertyName = reader.GetString();
                reader.Read(); // Move to the property value.

                switch (propertyName)
                {
                    case nameof(ModuleConfigurationFile.BasePath):
                        {
                            config.BasePath = JsonSerializer.Deserialize<string>(ref reader, options);

                            //Set to PWD if "." is specified
                            if (config.BasePath == ".")
                                config.BasePath = System.IO.Directory.GetCurrentDirectory();
                        }
                        break;
                    case nameof(ModuleConfigurationFile.Modules):
                        var modules = JsonSerializer.Deserialize<List<JsonElement>>(ref reader, options);
                        if (modules != null)
                        {
                            config.Modules = new List<ModuleConfiguration>(modules.Count);
                            foreach (var element in modules)
                            {
                                var moduleJson = element.GetRawText();
                                var module = JsonSerializer.Deserialize<ModuleConfiguration>(moduleJson, options);
                                module.BasePath = config.BasePath ?? "";
                                config.Modules.Add(module);
                            }
                        }
                        break;
                    default:
                        throw new JsonException($"Property '{propertyName}' is not supported.");
                }
            }

            throw new JsonException("Expected a JSON object end.");
        }

        public override void Write(Utf8JsonWriter writer, ModuleConfigurationFile value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("This converter does not support writing JSON.");
        }
    }
}
