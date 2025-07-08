using System.Collections.Generic;
using System.Text.Json;
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

        public Task<JsonDocument> RequestJsonAsync(string path)
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
                "jdev/sps/getcustomuserfields" => FieldsJson,
                _ => throw new System.InvalidOperationException(path)
            };
            return Task.FromResult(JsonDocument.Parse(json));
        }

        public Task<KeyInfo> GetKey2Async(string user) => throw new System.NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info) => throw new System.NotImplementedException();
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
        var del = await svc.DeleteUserAsync("1");
        var add = await svc.AssignUserToGroupAsync("1", "g1");
        var rem = await svc.RemoveUserFromGroupAsync("1", "g1");
        var created = await svc.AddUserAsync(new AddUser { Name = "x" });
        var edit = await svc.EditUserAsync(new EditUser { Name = "x", Uuid = "1" });

        Assert.Equal("admin", user.Name);
        Assert.Single(groups);
        Assert.Equal(UserGroupType.AdminDeprecated, groups[0].Type);
        Assert.Equal("u1", uuid);
        Assert.Equal(200, del.Code);
        Assert.Equal(200, add.Code);
        Assert.Equal(200, rem.Code);
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
}
