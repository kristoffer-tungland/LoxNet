namespace LoxNet.OperationModes;

/// <summary>
/// Provides query serialization for calendar mode attributes used when creating or updating entries.
/// </summary>
public interface ICalendarModeOption
{
    /// <summary>The calendar mode represented by this option.</summary>
    CalendarMode Mode { get; }

    /// <summary>Serializes the option to the calendar mode attribute string.</summary>
    string ToQueryAttribute();
}
