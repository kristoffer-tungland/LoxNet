namespace LoxNet.OperationModes;

/// <summary>
/// Occurrence of a weekday within a month used for schedule options.
/// Values correspond to the Operating Mode Schedule documentation.
/// </summary>
public enum WeekdayOccurrence
{
    /// <summary>Every occurrence.</summary>
    Every = 0,
    /// <summary>First occurrence.</summary>
    First = 1,
    /// <summary>Second occurrence.</summary>
    Second = 2,
    /// <summary>Third occurrence.</summary>
    Third = 3,
    /// <summary>Fourth occurrence.</summary>
    Fourth = 4,
    /// <summary>Last occurrence.</summary>
    Last = 5
}
