namespace LoxNet.OperationModes;

using System.Text.Json.Serialization;

/// <summary>
/// DTO representing an entry in the operating mode schedule.
/// </summary>
public record OperatingModeEntryDto(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("operatingMode")] string OperatingMode,
    [property: JsonPropertyName("calMode")] CalendarMode CalendarMode,
    [property: JsonPropertyName("calModeAttr")] string CalendarModeAttribute);
