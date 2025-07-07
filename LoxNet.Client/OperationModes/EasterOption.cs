namespace LoxNet.OperationModes;

/// <summary>
/// Represents <see cref="CalendarMode.Easter"/> attributes.
/// </summary>
public record EasterOption(int Offset) : ICalendarModeOption
{
    /// <inheritdoc />
    public CalendarMode Mode => CalendarMode.Easter;

    /// <inheritdoc />
    public string ToQueryAttribute() => Offset.ToString();
}
