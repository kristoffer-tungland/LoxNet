namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.SpecificTimespan"/> attributes.
/// </summary>
public record SpecificTimespanOption(int StartYear, CalendarMonth StartMonth, int StartDay, int EndYear, CalendarMonth EndMonth, int EndDay) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.SpecificTimespan;

    /// <inheritdoc />
    public string ToQueryAttribute() => $"{StartYear}/{(int)StartMonth}/{StartDay}/{EndYear}/{(int)EndMonth}/{EndDay}";
}
