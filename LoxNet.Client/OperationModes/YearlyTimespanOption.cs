namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.YearlyTimespan"/> attributes.
/// </summary>
public record YearlyTimespanOption(CalendarMonth StartMonth, int StartDay, CalendarMonth EndMonth, int EndDay) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.YearlyTimespan;

    /// <inheritdoc />
    public string ToQueryAttribute() => $"{(int)StartMonth}/{StartDay}/{(int)EndMonth}/{EndDay}";
}
