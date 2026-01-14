using LoxNet.Bridge.Config;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bridge.Tests;

public class DedupingTests
{
    private readonly MappingSection _mapping = new()
    {
        Name = "Test",
        Kind = "dimmer",
        LoxoneUuidAction = Guid.NewGuid().ToString(),
        MqttTopic = "zigbee2mqtt/test"
    };

    [Fact]
    public async Task MqttEchoIsSuppressed()
    {
        var config = new BridgeConfig { Mappings = new List<MappingSection> { _mapping } };
        var cache = new StateCache();
        var comparer = new StateComparer(config);
        var fakeLoxone = new FakeLoxoneExecutor();
        var fakePublisher = new FakeMqttPublisher();
        var engine = new SyncEngine(config, cache, comparer, new LoxoneCommandBuilder(), fakeLoxone, new Z2mPublisher(new Converters()), fakePublisher, NullLogger<SyncEngine>.Instance);

        var state = new NormalizedLightState(true, 50, null, null);
        cache.GetOrCreate(_mapping).LastSentToLoxone = state;

        await engine.ProcessMqttAsync(_mapping.MqttTopic, state, CancellationToken.None);

        Assert.Empty(fakeLoxone.Commands);
    }

    [Fact]
    public async Task LoxoneEchoIsSuppressed()
    {
        var config = new BridgeConfig { Mappings = new List<MappingSection> { _mapping } };
        var cache = new StateCache();
        var comparer = new StateComparer(config);
        var fakeLoxone = new FakeLoxoneExecutor();
        var fakePublisher = new FakeMqttPublisher();
        var engine = new SyncEngine(config, cache, comparer, new LoxoneCommandBuilder(), fakeLoxone, new Z2mPublisher(new Converters()), fakePublisher, NullLogger<SyncEngine>.Instance);

        var state = new NormalizedLightState(true, 50, null, null);
        cache.GetOrCreate(_mapping).LastSentToMqtt = state;

        await engine.ProcessLoxoneAsync(_mapping.LoxoneUuidAction, state, CancellationToken.None);

        Assert.Empty(fakePublisher.PublishedStates);
    }

    private sealed class FakeLoxoneExecutor : ILoxoneCommandExecutor
    {
        public List<string> Commands { get; } = new();

        public Task SendCommandsAsync(MappingSection mapping, IEnumerable<string> commands, CancellationToken cancellationToken)
        {
            Commands.AddRange(commands);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMqttPublisher : IMqttPublisher
    {
        public List<NormalizedLightState> PublishedStates { get; } = new();

        public bool IsConnected { get; set; } = true;

        public Task PublishAsync(MappingSection mapping, NormalizedLightState desired, NormalizedLightState lastSent, CancellationToken cancellationToken)
        {
            PublishedStates.Add(desired);
            return Task.CompletedTask;
        }
    }
}
