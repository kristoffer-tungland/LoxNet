namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.SpecificDate"/> attributes.
/// </summary>
public record SpecificDateOption(int Year, CalendarMonth Month, int Day) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.SpecificDate;

    /// <inheritdoc />
    public string ToQueryAttribute() => $"{Year}/{(int)Month}/{Day}";
}
