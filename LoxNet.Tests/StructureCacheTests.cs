using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace LoxNet.Tests;

public class StructureCacheTests
{
    private const string SampleJson = """
{
  "controls": {
    "uuid-1": {
      "name": "Light",
      "type": "Switch",
      "uuidAction": "act-uuid-1",
      "defaultRating": 2,
      "isSecured": false,
      "room": "room-1",
      "cat": "cat-1",
      "states": { "active": "uuid-1-state" },
      "subControls": {
        "sub-uuid1": { "name": "Sub Switch", "type": "Switch", "uuidAction": "sub-act-uuid1" }
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

    private class MockHttpClient : ILoxoneHttpClient
    {
        private readonly JsonDocument _doc = JsonDocument.Parse(SampleJson);
        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public TokenInfo? LastToken => null;
        public Task<JsonDocument> RequestJsonAsync(string path) => Task.FromResult(_doc);
        public Task<KeyInfo> GetKey2Async(string user) => throw new NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info) => throw new NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task LoadAsync_ParsesStructure()
    {
        var cache = new LoxoneStructureCache(new MockHttpClient());
        await cache.LoadAsync();

        Assert.True(cache.TryGetControl("uuid-1", out var ctrl));
        Assert.Equal("Light", ctrl!.Name);
        Assert.Equal("Switch", ctrl.Type);
        Assert.Equal(2, ctrl.DefaultRating);
        Assert.False(ctrl.IsSecured);
        Assert.True(cache.TryGetControl("sub-uuid1", out var sub));
        Assert.Equal("Sub Switch", sub!.Name);
        Assert.Equal("Switch", sub.Type);

        var room = cache.Rooms["room-1"];
        Assert.Equal("Kitchen", room.Name);
        Assert.Equal("room.png", room.Image);
        Assert.Equal(1, room.DefaultRating);

        var cat = cache.Categories["cat-1"];
        Assert.Equal("Lighting", cat.Name);
        Assert.Equal("lights", cat.Type);
        Assert.Equal("#0000ff", cat.Color);
    }
}
