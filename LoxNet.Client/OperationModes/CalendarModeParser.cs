namespace LoxNet.OperationModes;

using System;

/// <summary>
/// Parses calendar mode attribute strings into concrete <see cref="ICalendarModeOption"/> instances.
/// </summary>
public static class CalendarModeParser
{
    /// <summary>
    /// Parses an attribute string according to the given calendar mode.
    /// </summary>
    /// <param name="mode">The calendar mode.</param>
    /// <param name="attribute">The attribute string from the schedule.</param>
    /// <returns>The parsed option.</returns>
    public static ICalendarModeOption Parse(CalendarMode mode, string attribute)
    {
        var parts = attribute.Split('/');
        return mode switch
        {
            CalendarMode.YearlyDate => new YearlyDateOption((CalendarMonth)int.Parse(parts[0]), int.Parse(parts[1])),
            CalendarMode.Easter => new EasterOption(int.Parse(attribute)),
            CalendarMode.SpecificDate => new SpecificDateOption(int.Parse(parts[0]), (CalendarMonth)int.Parse(parts[1]), int.Parse(parts[2])),
            CalendarMode.SpecificTimespan => new SpecificTimespanOption(
                int.Parse(parts[0]),
                (CalendarMonth)int.Parse(parts[1]),
                int.Parse(parts[2]),
                int.Parse(parts[3]),
                (CalendarMonth)int.Parse(parts[4]),
                int.Parse(parts[5])),
            CalendarMode.YearlyTimespan => new YearlyTimespanOption(
                (CalendarMonth)int.Parse(parts[0]),
                int.Parse(parts[1]),
                (CalendarMonth)int.Parse(parts[2]),
                int.Parse(parts[3])),
            CalendarMode.Weekday => new WeekdayOption(
                (CalendarMonth)int.Parse(parts[0]),
                (CalendarWeekday)int.Parse(parts[1]),
                (WeekdayOccurrence)int.Parse(parts[2])),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
