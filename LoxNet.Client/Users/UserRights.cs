namespace LoxNet.Users;

using System;

/// <summary>
/// Flags representing permissions a user or group can have.
/// Values correspond to the official user management specification.
/// </summary>
[Flags]
public enum UserRights
{
    /// <summary>No additional rights.</summary>
    None = 0x00000000,
    /// <summary>Access to the web interface.</summary>
    Web = 0x00000001,
    /// <summary>Permission to configure the Miniserver using Loxone Config.</summary>
    LoxoneConfig = 0x00000004,
    /// <summary>Access to the FTP service.</summary>
    Ftp = 0x00000008,
    /// <summary>Access via Telnet.</summary>
    Telnet = 0x00000010,
    /// <summary>Permission to change operating modes.</summary>
    OperatingModes = 0x00000020,
    /// <summary>Permission to modify autopilot settings.</summary>
    Autopilot = 0x00000040,
    /// <summary>Expert mode Light access.</summary>
    ExpertModeLight = 0x00000080,
    /// <summary>Ability to manage other users.</summary>
    UserManagement = 0x00000100,
    /// <summary>All possible rights.</summary>
    All = 0x00FFFFFF
}
