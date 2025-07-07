namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.Weekday"/> attributes.
/// </summary>
public record WeekdayOption(CalendarMonth Month, CalendarWeekday Weekday, WeekdayOccurrence WeekdayInMonth) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.Weekday;

    /// <inheritdoc />
    public string ToQueryAttribute() => $"{(int)Month}/{(int)Weekday}/{(int)WeekdayInMonth}";
}
