using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// <returns>A message indicating the result of the operation.</returns>
    Task<LoxoneMessage> DeleteUserAsync(string uuid);

    /// <summary>
    /// Adds a user to a group.
    /// </summary>
    /// <param name="userUuid">The user's UUID.</param>
    /// <param name="groupUuid">The group's UUID.</param>
    /// <returns>A message indicating the result of the assignment.</returns>
    Task<LoxoneMessage> AssignUserToGroupAsync(string userUuid, string groupUuid);

    /// <summary>
    /// Removes a user from a group.
    /// </summary>
    /// <param name="userUuid">The user's UUID.</param>
    /// <param name="groupUuid">The group's UUID.</param>
    /// <returns>A message describing the outcome.</returns>
    Task<LoxoneMessage> RemoveUserFromGroupAsync(string userUuid, string groupUuid);

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
}
