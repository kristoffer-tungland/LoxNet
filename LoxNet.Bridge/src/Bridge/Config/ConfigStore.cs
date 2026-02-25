namespace LoxNet.Bridge.Config;

/// <summary>
/// Singleton that holds the live in-memory <see cref="BridgeConfig"/> and persists
/// it to the YAML file whenever it is updated.
/// </summary>
public sealed class ConfigStore
{
    private BridgeConfig _current;
    private readonly ConfigFileSettings _settings;

    public ConfigStore(BridgeConfig initial, ConfigFileSettings settings)
    {
        _current = initial;
        _settings = settings;
    }

    /// <summary>Gets the current in-memory configuration.</summary>
    public BridgeConfig Current => _current;

    /// <summary>
    /// Replaces the in-memory config with <paramref name="newConfig"/>, then
    /// serialises it to the YAML file on disk.
    /// </summary>
    public async Task UpdateAndSaveAsync(BridgeConfig newConfig)
    {
        _current = newConfig;
        var yaml = ConfigYamlSerializer.Serialize(newConfig);
        var directory = Path.GetDirectoryName(_settings.Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_settings.Path, yaml).ConfigureAwait(false);
    }
}
