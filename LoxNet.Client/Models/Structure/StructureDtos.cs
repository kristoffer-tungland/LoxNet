using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Represents the root structure of a <c>LoxApp3.json</c> file.
/// </summary>
internal class StructureFileDto
{
    /// <summary>Timestamp when the configuration was last modified.</summary>
    [JsonPropertyName("lastModified")]
    public string? LastModified { get; set; }

    /// <summary>Dictionary of operating modes keyed by identifier (name stored as string value).</summary>
    [JsonPropertyName("operatingModes")]
    [JsonConverter(typeof(OperatingModesConverter))]
    public Dictionary<string, string>? OperatingModes { get; set; }

    /// <summary>Dictionary of controls keyed by UUID.</summary>
    [JsonPropertyName("controls")]
    [JsonConverter(typeof(TolerantDictionaryConverter<ControlDto>))]
    public Dictionary<string, ControlDto>? Controls { get; set; }

    /// <summary>Dictionary of rooms keyed by identifier.</summary>
    [JsonPropertyName("rooms")]
    [JsonConverter(typeof(TolerantDictionaryConverter<RoomDto>))]
    public Dictionary<string, RoomDto>? Rooms { get; set; }

    /// <summary>Dictionary of categories keyed by identifier.</summary>
    [JsonPropertyName("cats")]
    [JsonConverter(typeof(TolerantDictionaryConverter<CategoryDto>))]
    public Dictionary<string, CategoryDto>? Categories { get; set; }
}

/// <summary>
/// Model for a control entry from the structure file.
/// </summary>
internal class ControlDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    [JsonPropertyName("cat")]
    public string? Category { get; set; }

    [JsonPropertyName("uuidAction")]
    public string? UuidAction { get; set; }

    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }

    [JsonPropertyName("isSecured")]
    public bool? IsSecured { get; set; }

    [JsonPropertyName("securedDetails")]
    public bool? SecuredDetails { get; set; }

    [JsonPropertyName("isFavorite")]
    public bool? IsFavorite { get; set; }

    [JsonPropertyName("defaultIcon")]
    public string? DefaultIcon { get; set; }

    [JsonPropertyName("states")]
    [JsonConverter(typeof(TolerantStringDictionaryConverter))]
    public Dictionary<string, string>? States { get; set; }

    [JsonPropertyName("details")]
    public JsonElement? Details { get; set; }

    [JsonPropertyName("statistic")]
    public JsonElement? Statistic { get; set; }

    [JsonPropertyName("restrictions")]
    public int? Restrictions { get; set; }

    [JsonPropertyName("hasControlNotes")]
    public bool? HasControlNotes { get; set; }

    [JsonPropertyName("preset")]
    public PresetDto? Preset { get; set; }

    [JsonPropertyName("links")]
    public List<string>? Links { get; set; }

    [JsonPropertyName("subControls")]
    public Dictionary<string, ControlDto>? SubControls { get; set; }
}

internal record PresetDto(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("name")] string? Name);

/// <summary>
/// Model for a room entry from the structure file.
/// </summary>
internal class RoomDto
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("isFavorite")]
    public bool? IsFavorite { get; set; }

    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }

    [JsonPropertyName("default")]
    public bool? Default { get; set; }
}

/// <summary>
/// Model for a category entry from the structure file.
/// </summary>
internal class CategoryDto
{
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }

    [JsonPropertyName("isFavorite")]
    public bool? IsFavorite { get; set; }

    [JsonPropertyName("default")]
    public bool? Default { get; set; }
}

internal sealed class OperatingModesConverter : JsonConverter<Dictionary<string, string>?>
{
    public override Dictionary<string, string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            var key = reader.GetString();
            if (key == null)
            {
                reader.Skip();
                continue;
            }

            reader.Read();
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    dictionary[key] = reader.GetString() ?? string.Empty;
                    break;
                case JsonTokenType.StartObject:
                {
                    using var doc = JsonDocument.ParseValue(ref reader);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.GetString() is { } name)
                    {
                        dictionary[key] = name;
                    }
                    break;
                }
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }

        writer.WriteEndObject();
    }
}

internal sealed class TolerantStringDictionaryConverter : JsonConverter<Dictionary<string, string>?>
{
    public override Dictionary<string, string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject, got {reader.TokenType}");

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected PropertyName, got {reader.TokenType}");

            var key = reader.GetString();
            if (key == null)
            {
                reader.Skip();
                continue;
            }

            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
            {
                dictionary[key] = reader.GetString() ?? string.Empty;
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException("Unexpected end of JSON");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }

        writer.WriteEndObject();
    }
}
