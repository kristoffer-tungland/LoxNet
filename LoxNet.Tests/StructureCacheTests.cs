using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using LoxNet;

namespace LoxNet.Tests;

public class StructureCacheTests
{
    private const string SampleJson = """
{
  "operatingModes": {
    "0": { "name": "Auto" },
    "1": { "name": "Home" },
    "2": { "name": "Away" }
  },
  "controls": {
    "uuid-1": {
      "name": "Light",
      "type": "Switch",
      "uuidAction": "act-uuid-1",
      "defaultRating": 2,
      "isSecured": false,
      "securedDetails": true,
      "statistic": { "frequency": 1 },
      "restrictions": 16,
      "hasControlNotes": true,
      "preset": { "uuid": "preset-uuid", "name": "Default" },
      "links": ["link-1"],
      "room": "room-1",
      "cat": "cat-1",
      "states": { "active": "uuid-1-state" },
      "details": { "isStairwayLs": true },
      "subControls": {
        "sub-uuid1": { "name": "Sub Switch", "type": "Switch", "uuidAction": "sub-act-uuid1" }
      }
    },
    "uuid-2": {
      "name": "LCV2",
      "type": "LightControllerV2",
      "uuidAction": "act-uuid-2",
      "room": "room-1",
      "states": { "activeMoods": "uuid-active", "moodList": "uuid-list" },
      "details": {
        "masterValue": "uuid-mv",
        "masterColor": "uuid-mc",
        "favoriteMoods": ["ID1"],
        "presence": 1
      }
    }

  },
  "rooms": {
    "room-1": { "name": "Kitchen", "image": "room.png", "defaultRating": 1 }
  },
  "cats": {
    "cat-1": { "name": "Lighting", "type": "lights", "color": "#0000ff" }
  }
}
""";

    private class MockWebSocketClient : ILoxoneWebSocketClient
    {
        public event EventHandler<string>? MessageReceived;
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoxoneMessage> AuthenticateWithTokenAsync(string token, string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task<LoxoneMessage> ConnectAndAuthenticateAsync(string user, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task KeepAliveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<LoxoneMessage> CommandAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(new LoxoneMessage(0, default, null));
        public Task ListenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Send(string json) => MessageReceived?.Invoke(this, json);
    }

    private class MockHttpClient : ILoxoneHttpClient
    {
        private readonly JsonDocument _doc = JsonDocument.Parse(SampleJson);
        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public TokenInfo? LastToken => null;
        public Task<JsonDocument> RequestJsonAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(_doc);
        public Task<KeyInfo> GetKey2Async(string user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task LoadAsync_ParsesStructure()
    {
        var cache = new LoxoneStructureState(new MockHttpClient());
        await cache.LoadAsync();

        Assert.True(cache.TryGetControl("uuid-1", out var ctrl));
        Assert.Equal("Light", ctrl!.Name);
        Assert.Equal(ControlType.Switch, ctrl.Type);
        Assert.IsType<SwitchControl>(ctrl);
        Assert.Equal("act-uuid-1", ctrl.UuidAction);
        Assert.True(ctrl.States!.ContainsKey("active"));
        var sw = Assert.IsType<SwitchControl>(ctrl);
        Assert.True(sw.Details!.IsStairwayLs);
        Assert.Equal("uuid-1-state", sw.ActiveState);
        Assert.Equal(2, ctrl.DefaultRating);
        Assert.False(ctrl.IsSecured);
        Assert.True(ctrl.SecuredDetails);
        Assert.True(ctrl.RawDetails.HasValue);
        Assert.True(ctrl.Statistic.HasValue);
        Assert.Equal(16, ctrl.Restrictions);
        Assert.True(ctrl.HasControlNotes);
        Assert.Equal("preset-uuid", ctrl.Preset!.Uuid);
        Assert.Contains("link-1", ctrl.Links!);
        Assert.True(ctrl.SubControls.ContainsKey("sub-uuid1"));
        var sub = Assert.IsType<SwitchControl>(ctrl.SubControls["sub-uuid1"]);
        Assert.Equal("Sub Switch", sub.Name);
        Assert.Equal(ControlType.Switch, sub.Type);

        var room = cache.Rooms["room-1"];
        Assert.Equal("Kitchen", room.Name);
        Assert.Equal("room.png", room.Image);
        Assert.Equal(1, room.DefaultRating);

        var cat = cache.Categories["cat-1"];
        Assert.Equal("Lighting", cat.Name);
        Assert.Equal("lights", cat.Type);
        Assert.Equal("#0000ff", cat.Color);

        var modes = cache.GetOperatingModes();
        Assert.Equal(3, modes.Count);
        Assert.Equal("Home", modes[1]);

        Assert.True(cache.TryGetControl("uuid-2", out var lcCtrl));
        var lc = Assert.IsType<LightControllerV2>(lcCtrl);
        Assert.Equal("LCV2", lc.Name);
        Assert.Equal("uuid-mv", lc.Details!.MasterValue);
        Assert.Contains("ID1", lc.Details.FavoriteMoods!);
        Assert.Equal("uuid-active", lc.ActiveMoodsState);
        Assert.Equal("uuid-list", lc.MoodListState);
    }

    [Fact]
    public async Task WebSocket_UpdatesState()
    {
        var ws = new MockWebSocketClient();
        var cache = new LoxoneStructureState(new MockHttpClient(), wsClient: ws);
        await cache.LoadAsync();

        Assert.True(cache.TryGetControl("uuid-1", out var ctrl));
        string? changedName = null;
        string? changedValue = null;
        ctrl!.StateChanged += (_, e) => { changedName = e.State; changedValue = e.Value; };

        ws.Send("{\"uuid\":\"uuid-1-state\",\"value\":\"1\"}");

        Assert.Equal("active", changedName);
        Assert.Equal("1", changedValue);
        Assert.Equal("1", ctrl.StateValues["active"]);
    }
}
