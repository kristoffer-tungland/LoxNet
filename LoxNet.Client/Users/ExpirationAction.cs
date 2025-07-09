namespace LoxNet.Users;

/// <summary>
/// Action taken when a user's validity expires.
/// </summary>
public enum ExpirationAction
{
    /// <summary>Deactivate the user when expired.</summary>
    Deactivate = 0,
    /// <summary>Delete the user when expired.</summary>
    Delete = 1
}
