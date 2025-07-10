using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LoxNet;
using LoxNet.Users;

namespace LoxNet.Tests;

public class UserServiceTests
{
    private class MockHttpClient : ILoxoneHttpClient
    {
        public List<string> Paths { get; } = new();
        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public TokenInfo? LastToken => null;

        private const string ListJson = "{\"LL\":{\"Code\":200,\"value\":[{\"name\":\"admin\",\"uuid\":\"1\",\"isAdmin\":true,\"userState\":0}]}}";
        private const string UserJson = "{\"LL\":{\"Code\":200,\"value\":{\"name\":\"admin\",\"uuid\":\"1\",\"userid\":\"123\",\"isAdmin\":true,\"userState\":0}}}";
        private const string GroupJson = "{\"LL\":{\"Code\":200,\"value\":[{\"name\":\"Group\",\"description\":\"desc\",\"uuid\":\"g1\",\"type\":1,\"userRights\":2}]}}";
        private const string UuidJson = "{\"LL\":{\"Code\":200,\"value\":\"u1\"}}";
        private const string OkJson = "{\"LL\":{\"Code\":200}}";
        private const string FieldsJson = "{\"LL\":{\"Code\":200,\"value\":{\"customField1\":\"Building\",\"customField2\":\"Space\"}}}";
        private const string OptionsJson = "{\"LL\":{\"Code\":200,\"value\":{\"company\":[\"Loxone\"]}}}";
        private const string LookupJson = "{\"LL\":{\"Code\":200,\"value\":{\"name\":\"admin\",\"uuid\":\"u1\"}}}";
        private const string PeersJson = "{\"LL\":{\"Code\":200,\"value\":{\"peers\":[{\"serial\":\"p1\",\"name\":\"Peer\",\"intAddr\":\"1\",\"extAddr\":\"e\"}]}}}";
        private const string DiscoverJson = "{\"LL\":{\"Code\":200,\"value\":{\"serial\":\"p1\",\"users\":[{\"name\":\"u\",\"uuid\":\"1\",\"used\":false}]}}}";
        private const string PermJson = "{\"LL\":{\"Code\":200,\"value\":{}}}";

        public Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            var json = path switch
            {
                "jdev/sps/getuserlist2" => ListJson,
                var p when p.StartsWith("jdev/sps/getuser/") => UserJson,
                "jdev/sps/getgrouplist" => GroupJson,
                var p when p.StartsWith("jdev/sps/createuser") => UuidJson,
                var p when p.StartsWith("jdev/sps/deleteuser") => OkJson,
                var p when p.StartsWith("jdev/sps/assignusertogroup") => OkJson,
                var p when p.StartsWith("jdev/sps/removeuserfromgroup") => OkJson,
                var p when p.StartsWith("jdev/sps/addoredituser") => UserJson,
                var p when p.StartsWith("jdev/sps/updateuserpwdh") => OkJson,
                var p when p.StartsWith("jdev/sps/updateuservisupwdh") => OkJson,
                var p when p.StartsWith("jdev/sps/updateuseraccesscode") => OkJson,
                var p when p.StartsWith("jdev/sps/addusernfc") => OkJson,
                var p when p.StartsWith("jdev/sps/removeusernfc") => OkJson,
                var p when p.StartsWith("jdev/sps/getcontrolpermissions") => PermJson,
                "jdev/sps/getuserpropertyoptions" => OptionsJson,
                var p when p.StartsWith("jdev/sps/checkuserid") => LookupJson,
                "jdev/sps/trustusermanagement/peers" => PeersJson,
                var p when p.StartsWith("jdev/sps/trustusermanagement/discover") => DiscoverJson,
                var p when p.StartsWith("jdev/sps/trustusermanagement/add") => OkJson,
                var p when p.StartsWith("jdev/sps/trustusermanagement/remove") => OkJson,
                var p when p.StartsWith("jdev/sps/trustusermanagement/edit") => OkJson,
                "jdev/sps/getcustomuserfields" => FieldsJson,
                _ => throw new System.InvalidOperationException(path)
            };
            return Task.FromResult(JsonDocument.Parse(json));
        }

        public Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default) => throw new System.NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetUsers_ReturnsList()
    {
        var client = new MockHttpClient();
        var svc = new UserService(client);

        var list = await svc.GetUsersAsync();

        Assert.Single(list);
        Assert.Equal("admin", list[0].Name);
        Assert.Equal(UserState.Enabled, list[0].UserState);
        Assert.Contains("jdev/sps/getuserlist2", client.Paths);
    }

    [Fact]
    public async Task GetCustomFieldLabels_ReturnsList()
    {
        var client = new MockHttpClient();
        var svc = new UserService(client);

        var labels = await svc.GetCustomFieldLabelsAsync();

        Assert.Equal("Building", labels[0]);
        Assert.Equal("Space", labels[1]);
        Assert.Contains("jdev/sps/getcustomuserfields", client.Paths);
    }

    [Fact]
    public async Task Commands_ReturnOk()
    {
        var client = new MockHttpClient();
        var svc = new UserService(client);

        var user = await svc.GetUserAsync("1");
        var groups = await svc.GetGroupsAsync();
        var uuid = await svc.CreateUserAsync("name");
        await svc.DeleteUserAsync("1");
        await svc.AssignUserToGroupAsync("1", "g1");
        await svc.RemoveUserFromGroupAsync("1", "g1");
        var created = await svc.AddUserAsync(new AddUser { Name = "x" });
        var edit = await svc.EditUserAsync(new EditUser { Name = "x", Uuid = "1" });

        Assert.Equal("admin", user.Name);
        Assert.Single(groups);
        Assert.Equal(UserGroupType.AdminDeprecated, groups[0].Type);
        Assert.Equal("u1", uuid);
        Assert.Equal("admin", created.Name);
        Assert.Equal("admin", edit.Name);
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/getuser/1"));
        Assert.Contains("jdev/sps/getgrouplist", client.Paths);
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/createuser"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/deleteuser/1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/assignusertogroup/1/g1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/removeuserfromgroup/1/g1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/addoredituser"));
    }

    [Fact]
    public async Task AdditionalCommands_Work()
    {
        var client = new MockHttpClient();
        var svc = new UserService(client);

        await svc.UpdateUserPasswordHashAsync("1", "h");
        await svc.UpdateUserVisuPasswordHashAsync("1", "h");
        await svc.UpdateUserAccessCodeAsync("1", "1234");
        await svc.AddUserNfcTagAsync("1", "n1", "t");
        await svc.RemoveUserNfcTagAsync("1", "n1");
        using var perms = await svc.GetControlPermissionsAsync("c1");
        var options = await svc.GetUserPropertyOptionsAsync();
        var lookup = await svc.CheckUserIdAsync("uid");
        var peers = await svc.GetTrustPeersAsync();
        var disc = await svc.DiscoverTrustUsersAsync("p1");
        await svc.TrustAddUserAsync("p1", "u1");
        await svc.TrustRemoveUserAsync("p1", "u1");
        await svc.TrustEditAsync("{}");

        Assert.Equal(200, perms.RootElement.GetProperty("LL").GetProperty("Code").GetInt32());
        Assert.Contains("Loxone", options["company"]);
        Assert.Equal("admin", lookup!.Name);
        Assert.Equal("Peer", peers[0].Name);
        Assert.Equal("p1", disc.Serial);

        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/updateuserpwdh/1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/updateuservisupwdh/1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/updateuseraccesscode/1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/addusernfc/1/n1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/removeusernfc/1/n1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/getcontrolpermissions/c1"));
        Assert.Contains("jdev/sps/getuserpropertyoptions", client.Paths);
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/checkuserid"));
        Assert.Contains("jdev/sps/trustusermanagement/peers", client.Paths);
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/trustusermanagement/discover/p1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/trustusermanagement/add/p1/u1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/trustusermanagement/remove/p1/u1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/trustusermanagement/edit/"));
    }
}
