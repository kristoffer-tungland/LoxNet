using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LoxNet.Bridge.Config;

public static class ConfigYamlSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static string Serialize(BridgeConfig config)
    {
        return Serializer.Serialize(config);
    }
}
