namespace LoxNet.OperationModes;

/// <summary>
/// Converts <see cref="OperatingModeEntryDto"/> instances to typed <see cref="OperatingModeEntry"/> objects.
/// </summary>
public static class OperatingModeEntryFactory
{
    /// <summary>
    /// Converts a DTO to a typed entry using the calendar mode parser.
    /// </summary>
    public static OperatingModeEntry FromDto(OperatingModeEntryDto dto)
    {
        var option = CalendarModeParser.Parse(dto.CalendarMode, dto.CalendarModeAttribute);
        return new OperatingModeEntry(dto.Uuid, dto.Name, dto.OperatingMode, option);
    }
}
