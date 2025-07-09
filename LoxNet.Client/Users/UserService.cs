using System;
using System.Text.Json;
using System.Threading;
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
    public async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getuserlist2", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var arr = msg.Value;
        return JsonSerializer.Deserialize<UserSummary[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetUserAsync(string uuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/getuser/{uuid}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var value = msg.Value;
        return JsonSerializer.Deserialize<UserDetails>(value.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getgrouplist", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var arr = msg.Value;
        return JsonSerializer.Deserialize<UserGroup[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<string> CreateUserAsync(string username, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/createuser/{Uri.EscapeDataString(username)}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        return msg.Value.GetString()!;
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string uuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/deleteuser/{uuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task AssignUserToGroupAsync(string userUuid, string groupUuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/assignusertogroup/{userUuid}/{groupUuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task RemoveUserFromGroupAsync(string userUuid, string groupUuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/removeuserfromgroup/{userUuid}/{groupUuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCustomFieldLabelsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getcustomuserfields", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var obj = msg.Value;
        return obj.EnumerateObject()
            .OrderBy(p => p.Name)
            .Select(p => p.Value.GetString()!)
            .ToArray();
    }

    /// <inheritdoc />
    public Task<UserDetails> AddUserAsync(AddUser user, CancellationToken cancellationToken = default) => SendAddOrEditAsync(user, cancellationToken);

    /// <inheritdoc />
    public Task<UserDetails> EditUserAsync(EditUser user, CancellationToken cancellationToken = default) => SendAddOrEditAsync(user, cancellationToken);

    private async Task<UserDetails> SendAddOrEditAsync(AddUser user, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(user);
        using var doc = await _client.RequestJsonAsync($"jdev/sps/addoredituser/{Uri.EscapeDataString(json)}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var value = msg.Value;
        return JsonSerializer.Deserialize<UserDetails>(value.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task UpdateUserPasswordHashAsync(string uuid, string hash, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuserpwdh/{uuid}/{hash}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task UpdateUserVisuPasswordHashAsync(string uuid, string hash, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuservisupwdh/{uuid}/{hash}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task UpdateUserAccessCodeAsync(string uuid, string accessCode, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/updateuseraccesscode/{uuid}/{accessCode}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task AddUserNfcTagAsync(string uuid, string nfcTagId, string name, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/addusernfc/{uuid}/{nfcTagId}/{Uri.EscapeDataString(name)}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task RemoveUserNfcTagAsync(string uuid, string nfcTagId, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/removeusernfc/{uuid}/{nfcTagId}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetControlPermissionsAsync(string uuid, CancellationToken cancellationToken = default)
    {
        var doc = await _client.RequestJsonAsync($"jdev/sps/getcontrolpermissions/{uuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
        return doc;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string[]>> GetUserPropertyOptionsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/getuserpropertyoptions", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var obj = msg.Value;
        var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
        {
            var arr = prop.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            dict[prop.Name] = arr;
        }
        return dict;
    }

    /// <inheritdoc />
    public async Task<UserLookup?> CheckUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/checkuserid/{Uri.EscapeDataString(userId)}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var value = msg.Value;
        if (value.TryGetProperty("uuid", out _))
        {
            return JsonSerializer.Deserialize<UserLookup>(value.GetRawText())!;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrustPeer>> GetTrustPeersAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/trustusermanagement/peers", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var arr = msg.Value.GetProperty("peers");
        return JsonSerializer.Deserialize<TrustPeer[]>(arr.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task<TrustDiscoveryResult> DiscoverTrustUsersAsync(string peerSerial, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/discover/{peerSerial}", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var value = msg.Value;
        return JsonSerializer.Deserialize<TrustDiscoveryResult>(value.GetRawText())!;
    }

    /// <inheritdoc />
    public async Task TrustAddUserAsync(string peerSerial, string userUuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/add/{peerSerial}/{userUuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task TrustRemoveUserAsync(string peerSerial, string userUuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/remove/{peerSerial}/{userUuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task TrustEditAsync(string json, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/trustusermanagement/edit/{Uri.EscapeDataString(json)}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

}
