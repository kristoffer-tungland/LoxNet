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

    /// <inheritdoc />
    public async Task<LoxoneMessage> UpdateUserPasswordHashAsync(string uuid, string hash)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuserpwdh/{uuid}/{hash}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> UpdateUserVisuPasswordHashAsync(string uuid, string hash)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuservisupwdh/{uuid}/{hash}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> UpdateUserAccessCodeAsync(string uuid, string accessCode)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuseraccesscode/{uuid}/{accessCode}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> AddUserNfcTagAsync(string uuid, string nfcTagId, string name)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/addusernfc/{uuid}/{nfcTagId}/{Uri.EscapeDataString(name)}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> RemoveUserNfcTagAsync(string uuid, string nfcTagId)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/removeusernfc/{uuid}/{nfcTagId}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetControlPermissionsAsync(string uuid)
    {
        return await _client.RequestJsonAsync($"jdev/sps/getcontrolpermissions/{uuid}");
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string[]>> GetUserPropertyOptionsAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getuserpropertyoptions");
        var obj = doc.RootElement.GetProperty("LL").GetProperty("value");
        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            var arr = prop.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            dict[prop.Name] = arr;
        }
        return dict;
    }

    /// <inheritdoc />
    public async Task<UserLookup?> CheckUserIdAsync(string userId)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/checkuserid/{Uri.EscapeDataString(userId)}");
        var value = doc.RootElement.GetProperty("LL").GetProperty("value");
        if (value.TryGetProperty("uuid", out _))
        {
            return JsonSerializer.Deserialize<UserLookup>(value.GetRawText())!;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrustPeer>> GetTrustPeersAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/trustusermanagement/peers");
        var arr = doc.RootElement.GetProperty("LL").GetProperty("value").GetProperty("peers");
        return JsonSerializer.Deserialize<TrustPeer[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<TrustDiscoveryResult> DiscoverTrustUsersAsync(string peerSerial)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/discover/{peerSerial}");
        var value = doc.RootElement.GetProperty("LL").GetProperty("value");
        return JsonSerializer.Deserialize<TrustDiscoveryResult>(value.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> TrustAddUserAsync(string peerSerial, string userUuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/add/{peerSerial}/{userUuid}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> TrustRemoveUserAsync(string peerSerial, string userUuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/remove/{peerSerial}/{userUuid}");
        return ParseMessage(doc);
    }

    /// <inheritdoc />
    public async Task<LoxoneMessage> TrustEditAsync(string json)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/edit/{Uri.EscapeDataString(json)}");
        return ParseMessage(doc);
    }

    private static LoxoneMessage ParseMessage(JsonDocument doc)
    {
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? msg = ll.TryGetProperty("message", out var m) ? m.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, msg);
    }
}
