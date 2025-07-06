using System;

namespace LoxNet;

/// <summary>
/// Event data for a changed state value.
/// </summary>
public class LoxoneStateChangedEventArgs : EventArgs
{
    /// <summary>Name of the state that changed.</summary>
    public string State { get; }

    /// <summary>New value of the state.</summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoxoneStateChangedEventArgs"/> class.
    /// </summary>
    public LoxoneStateChangedEventArgs(string state, string value)
    {
        State = state;
        Value = value;
    }
}
