namespace LoxNet.Users;

/// <summary>
/// Classification of a user group as returned by the API.
/// </summary>
public enum UserGroupType
{
    /// <summary>Normal group.</summary>
    Normal = 0,
    /// <summary>Admin group (deprecated).</summary>
    AdminDeprecated = 1,
    /// <summary>Group containing all users.</summary>
    All = 2,
    /// <summary>Group for no users.</summary>
    None = 3,
    /// <summary>All-access administrative group.</summary>
    AllAccess = 4
}
