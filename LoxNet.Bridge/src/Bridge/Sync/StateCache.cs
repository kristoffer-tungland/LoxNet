using System.Collections.Concurrent;
using LoxNet.Bridge.Config;

namespace LoxNet.Bridge.Sync;

public class StateCache
{
    private readonly ConcurrentDictionary<string, MappingState> _states = new(StringComparer.OrdinalIgnoreCase);

    public MappingState GetOrCreate(MappingSection mapping)
    {
        return _states.GetOrAdd(mapping.MqttTopic, _ => new MappingState(mapping));
    }

    public IEnumerable<MappingState> AllStates => _states.Values;
}

public class MappingState
{
    public MappingState(MappingSection mapping)
    {
        Mapping = mapping;
    }

    public MappingSection Mapping { get; }

    public NormalizedLightState LastObservedMqtt { get; set; } = NormalizedLightState.Empty;

    public NormalizedLightState LastObservedLoxone { get; set; } = NormalizedLightState.Empty;

    public NormalizedLightState LastSentToMqtt { get; set; } = NormalizedLightState.Empty;

    public NormalizedLightState LastSentToLoxone { get; set; } = NormalizedLightState.Empty;
}
