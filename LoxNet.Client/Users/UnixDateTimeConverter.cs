using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxNet.Users;

/// <summary>
/// Converts Unix timestamps in seconds to <see cref="DateTime"/> values.
/// </summary>
public class UnixDateTimeConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteNumberValue(((DateTimeOffset)value).ToUnixTimeSeconds());
}

/// <summary>
/// Handles nullable DateTime values represented as Unix timestamps.
/// </summary>
public class NullableUnixDateTimeConverter : JsonConverter<DateTime?>
{
    /// <inheritdoc />
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null
            ? (DateTime?)null
            : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(((DateTimeOffset)value.Value).ToUnixTimeSeconds());
        }
    }
}
