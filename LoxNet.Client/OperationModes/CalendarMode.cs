namespace LoxNet.OperationModes;

/// <summary>
/// Specifies when a scheduled operating mode becomes active.
/// Values correspond to the Operating Mode Schedule documentation.
/// </summary>
public enum CalendarMode
{
    /// <summary>Specific date repeated every year.</summary>
    YearlyDate = 0,
    /// <summary>Date relative to Easter.</summary>
    Easter = 1,
    /// <summary>One specific date.</summary>
    SpecificDate = 2,
    /// <summary>Specific period between two dates.</summary>
    SpecificTimespan = 3,
    /// <summary>Repeated yearly timespan.</summary>
    YearlyTimespan = 4,
    /// <summary>One or more weekdays in specific months.</summary>
    Weekday = 5
}
