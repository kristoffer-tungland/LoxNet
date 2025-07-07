namespace LoxNet.OperationModes;

/// <summary>
/// Represents an entry in the operating mode schedule with a typed calendar mode option.
/// </summary>
public record OperatingModeEntry(string Uuid, string Name, string OperatingMode, ICalendarModeOption Mode);
