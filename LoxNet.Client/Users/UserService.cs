using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using LoxNet;

namespace LoxNet.Users;

/// <summary>
/// Default implementation of <see cref="IUserService"/> using <see cref="ILoxoneHttpClient"/>.
/// </summary>
public class UserService : IUserService
{
    private readonly ILoxoneHttpClient _client;

    /// <summary>Creates the service.</summary>
    public UserService(ILoxoneHttpClient httpClient)
    {
        _client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }


    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getuserlist2");
        var arr = doc.RootElement.GetProperty("LL").GetProperty("value");
        return JsonSerializer.Deserialize<UserSummary[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetUserAsync(string uuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/getuser/{uuid}");
        var value = doc.RootElement.GetProperty("LL").GetProperty("value");
        return JsonSerializer.Deserialize<UserDetails>(value.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserGroup>> GetGroupsAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getgrouplist");
        var arr = doc.RootElement.GetProperty("LL").GetProperty("value");
        return JsonSerializer.Deserialize<UserGroup[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<string> CreateUserAsync(string username)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/createuser/{Uri.EscapeDataString(username)}");
        return doc.RootElement.GetProperty("LL").GetProperty("value").GetString()!;
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> DeleteUserAsync(string uuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/deleteuser/{uuid}");
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? msg = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, msg);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> AssignUserToGroupAsync(string userUuid, string groupUuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/assignusertogroup/{userUuid}/{groupUuid}");
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? msg = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, msg);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> RemoveUserFromGroupAsync(string userUuid, string groupUuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/removeuserfromgroup/{userUuid}/{groupUuid}");
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? msg = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, msg);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCustomFieldLabelsAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getcustomuserfields");
        var obj = doc.RootElement.GetProperty("LL").GetProperty("value");
        return obj.EnumerateObject()
            .OrderBy(p => p.Name)
            .Select(p => p.Value.GetString()!)
            .ToArray();
    }

    /// <inheritdoc />
    public Task<UserDetails> AddUserAsync(AddUser user) => SendAddOrEditAsync(user);

    /// <inheritdoc />
    public Task<UserDetails> EditUserAsync(EditUser user) => SendAddOrEditAsync(user);

    private async Task<UserDetails> SendAddOrEditAsync(AddUser user)
    {
        var json = JsonSerializer.Serialize(user);
        using var doc = await _client.RequestJsonAsync($"jdev/sps/addoredituser/{Uri.EscapeDataString(json)}");
        var value = doc.RootElement.GetProperty("LL").GetProperty("value");
        return JsonSerializer.Deserialize<UserDetails>(value.GetRawText())!;
    }
}
