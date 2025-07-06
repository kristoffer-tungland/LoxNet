namespace LoxNet;

/// <summary>
/// Enumerates known Loxone control types used by the client.
/// </summary>
public enum ControlType
{
    /// <summary>Unrecognized control type.</summary>
    Unknown,
    /// <summary>A generic switch.</summary>
    Switch,
    /// <summary>A lighting controller.</summary>
    LightController,
    /// <summary>Second version of the lighting controller.</summary>
    LightControllerV2
}
