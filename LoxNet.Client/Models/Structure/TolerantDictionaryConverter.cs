using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LoxNet;

/// <summary>
/// Shared logger for tolerant dictionary converters.
/// </summary>
internal static class TolerantDictionaryConverterLogger
{
    internal static ILogger? Current { get; set; }
}

/// <summary>
/// JSON converter that tolerates deserialization errors in dictionary values.
/// Logs and skips invalid entries instead of failing the entire operation.
/// </summary>
internal class TolerantDictionaryConverter<TValue> : JsonConverter<Dictionary<string, TValue>?>
{
    public override Dictionary<string, TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");

        var dictionary = new Dictionary<string, TValue>();
        var valueName = typeof(TValue).Name;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            var key = reader.GetString();
            if (key == null)
            {
                TolerantDictionaryConverterLogger.Current?.LogWarning("[StructureLoad] Skipping dictionary entry with null key in {ValueType}", valueName);
                reader.Skip();
                continue;
            }

            try
            {
                reader.Read();
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.ValueKind == JsonValueKind.Null)
                {
                    TolerantDictionaryConverterLogger.Current?.LogWarning("[StructureLoad] Skipping {ValueType} with key '{Key}': value is null", valueName, key);
                    continue;
                }

                var value = JsonSerializer.Deserialize<TValue>(doc.RootElement.GetRawText(), options);
                if (value != null)
                {
                    dictionary[key] = value;
                }
                else
                {
                    TolerantDictionaryConverterLogger.Current?.LogWarning("[StructureLoad] Skipping {ValueType} with key '{Key}': deserialized to null", valueName, key);
                }
            }
            catch (JsonException ex)
            {
                TolerantDictionaryConverterLogger.Current?.LogWarning(ex, "[StructureLoad] Skipping {ValueType} with key '{Key}' at path {Path}: {Error}",
                    valueName, key, ex.Path, ex.Message);
                // Continue processing remaining entries
            }
            catch (Exception ex)
            {
                TolerantDictionaryConverterLogger.Current?.LogWarning(ex, "[StructureLoad] Skipping {ValueType} with key '{Key}': {Error}",
                    valueName, key, ex.Message);
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, TValue>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }
        writer.WriteEndObject();
    }
}
