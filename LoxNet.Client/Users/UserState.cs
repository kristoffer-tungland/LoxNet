namespace LoxNet.Users;

/// <summary>
/// Indicates whether a user is enabled and within which timeframe.
/// Values correspond to the official user management specification.
/// </summary>
public enum UserState
{
    /// <summary>User is enabled without time limitation.</summary>
    Enabled = 0,
    /// <summary>User is disabled.</summary>
    Disabled = 1,
    /// <summary>User is enabled until a specific timestamp.</summary>
    EnabledUntil = 2,
    /// <summary>User is enabled only after a specific timestamp.</summary>
    EnabledFrom = 3,
    /// <summary>User is enabled within a time span.</summary>
    EnabledBetween = 4
}
