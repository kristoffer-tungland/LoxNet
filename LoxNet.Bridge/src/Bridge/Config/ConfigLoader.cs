using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LoxNet.Bridge.Config;

public static class ConfigLoader
{
    public static async Task<BridgeConfig> LoadAsync(IConfiguration configuration, string? configPathFromEnv)
    {
        var path = ResolvePath(configuration, configPathFromEnv);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config file not found at {path}");
        }

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        var yaml = await reader.ReadToEndAsync().ConfigureAwait(false);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<BridgeConfig>(yaml) ?? new BridgeConfig();
        config.Validate();
        return config;
    }

    public static string ResolvePath(IConfiguration configuration, string? configPathFromEnv)
    {
        var path = configPathFromEnv;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = configuration["CONFIG"];
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/data/config.yaml";
        }

        return path;
    }
}
