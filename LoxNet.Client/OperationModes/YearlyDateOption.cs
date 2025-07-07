namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.YearlyDate"/> attributes.
/// </summary>
public record YearlyDateOption(CalendarMonth Month, int Day) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.YearlyDate;

    /// <inheritdoc />
    public string ToQueryAttribute() => $"{(int)Month}/{Day}";
}
