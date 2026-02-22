namespace LoxNet.Bridge.Ui.Models;

public class LoxoneSubcontrolOption
{
    public required string ParentName { get; init; }
    public required string SubcontrolName { get; init; }
    public required string Type { get; init; }
    public required string UuidAction { get; init; }
}

public class MqttLightOption
{
    public required string FriendlyName { get; init; }
    public required string Topic { get; init; }
    public required string Model { get; init; }
}
