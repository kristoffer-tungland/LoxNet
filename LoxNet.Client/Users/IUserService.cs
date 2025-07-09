using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using LoxNet;

namespace LoxNet.Users;

/// <summary>
/// Provides access to user related endpoints.
/// </summary>
public interface IUserService
{

    /// <summary>
    /// Retrieves a list of configured users.
    /// </summary>
    /// <returns>A collection of <see cref="UserSummary"/> records.</returns>
    Task<IReadOnlyList<UserSummary>> GetUsersAsync();

    /// <summary>
    /// Gets the full configuration for a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <returns>The <see cref="UserDetails"/> for the user.</returns>
    Task<UserDetails> GetUserAsync(string uuid);

    /// <summary>
    /// Gets the list of available user groups.
    /// </summary>
    /// <returns>A collection of <see cref="UserGroup"/> items.</returns>
    Task<IReadOnlyList<UserGroup>> GetGroupsAsync();

    /// <summary>
    /// Creates a new user by username.
    /// </summary>
    /// <param name="username">The name of the new user.</param>
    /// <returns>The UUID of the created user.</returns>
    Task<string> CreateUserAsync(string username);

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user to delete.</param>
    /// <returns>A task that completes when the user is removed.</returns>
    Task DeleteUserAsync(string uuid);

    /// <summary>
    /// Adds a user to a group.
    /// </summary>
    /// <param name="userUuid">The user's UUID.</param>
    /// <param name="groupUuid">The group's UUID.</param>
    /// <returns>A task that completes when the assignment is done.</returns>
    Task AssignUserToGroupAsync(string userUuid, string groupUuid);

    /// <summary>
    /// Removes a user from a group.
    /// </summary>
    /// <param name="userUuid">The user's UUID.</param>
    /// <param name="groupUuid">The group's UUID.</param>
    /// <returns>A task that completes when the user is removed.</returns>
    Task RemoveUserFromGroupAsync(string userUuid, string groupUuid);

    /// <summary>
    /// Retrieves the labels for configurable custom user fields.
    /// </summary>
    /// <returns>An ordered list of field labels.</returns>
    Task<IReadOnlyList<string>> GetCustomFieldLabelsAsync();

    /// <summary>
    /// Adds a new user using the provided configuration.
    /// </summary>
    /// <param name="user">The configuration of the user to create.</param>
    /// <returns>The created <see cref="UserDetails"/> returned by the server.</returns>
    Task<UserDetails> AddUserAsync(AddUser user);

    /// <summary>
    /// Updates an existing user using the provided configuration.
    /// </summary>
    /// <param name="user">The configuration of the user to edit.</param>
    /// <returns>The updated <see cref="UserDetails"/> returned by the server.</returns>
    Task<UserDetails> EditUserAsync(EditUser user);

    /// <summary>
    /// Updates the hashed password of a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <param name="hash">The new hashed password value.</param>
    /// <returns>A task that completes when the password is updated.</returns>
    Task UpdateUserPasswordHashAsync(string uuid, string hash);

    /// <summary>
    /// Updates the hashed visualization password of a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <param name="hash">The new hashed visu password.</param>
    /// <returns>A task that completes when the password is updated.</returns>
    Task UpdateUserVisuPasswordHashAsync(string uuid, string hash);

    /// <summary>
    /// Updates the access code of a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <param name="accessCode">The new numeric access code.</param>
    /// <returns>A task that completes when the code is updated.</returns>
    Task UpdateUserAccessCodeAsync(string uuid, string accessCode);

    /// <summary>
    /// Links an NFC tag with a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <param name="nfcTagId">The tag identifier.</param>
    /// <param name="name">The label of the tag.</param>
    /// <returns>A task that completes when the tag is added.</returns>
    Task AddUserNfcTagAsync(string uuid, string nfcTagId, string name);

    /// <summary>
    /// Removes an NFC tag from a user.
    /// </summary>
    /// <param name="uuid">The UUID of the user.</param>
    /// <param name="nfcTagId">The tag identifier.</param>
    /// <returns>A task that completes when the tag is removed.</returns>
    Task RemoveUserNfcTagAsync(string uuid, string nfcTagId);

    /// <summary>
    /// Retrieves permissions assigned directly to a control.
    /// </summary>
    /// <param name="uuid">The control UUID.</param>
    /// <returns>The raw permissions document returned by the server.</returns>
    Task<JsonDocument> GetControlPermissionsAsync(string uuid);

    /// <summary>
    /// Gets all configured values for user properties.
    /// </summary>
    /// <returns>Dictionary of property name to configured values.</returns>
    Task<Dictionary<string, string[]>> GetUserPropertyOptionsAsync();

    /// <summary>
    /// Looks up a user by the configured user ID.
    /// </summary>
    /// <param name="userId">The user ID to search for.</param>
    /// <returns>The matching user or <c>null</c> if not found.</returns>
    Task<UserLookup?> CheckUserIdAsync(string userId);

    /// <summary>
    /// Retrieves the list of peers available for trust user management.
    /// </summary>
    /// <returns>List of peers.</returns>
    Task<IReadOnlyList<TrustPeer>> GetTrustPeersAsync();

    /// <summary>
    /// Discovers users of a peer.
    /// </summary>
    /// <param name="peerSerial">The serial of the peer.</param>
    /// <returns>The discovery result.</returns>
    Task<TrustDiscoveryResult> DiscoverTrustUsersAsync(string peerSerial);

    /// <summary>
    /// Adds a user from a peer via trust management.
    /// </summary>
    /// <param name="peerSerial">The peer serial.</param>
    /// <param name="userUuid">UUID of the user to add.</param>
    /// <returns>A task that completes when the user is added.</returns>
    Task TrustAddUserAsync(string peerSerial, string userUuid);

    /// <summary>
    /// Removes a user from a peer via trust management.
    /// </summary>
    /// <param name="peerSerial">The peer serial.</param>
    /// <param name="userUuid">UUID of the user to remove.</param>
    /// <returns>A task that completes when the user is removed.</returns>
    Task TrustRemoveUserAsync(string peerSerial, string userUuid);

    /// <summary>
    /// Applies a trust user edit payload.
    /// </summary>
    /// <param name="json">The JSON body describing the changes.</param>
    /// <returns>A task that completes when the edit is applied.</returns>
    Task TrustEditAsync(string json);
}
