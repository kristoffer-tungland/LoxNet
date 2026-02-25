using System.ComponentModel.DataAnnotations;

namespace LoxNet.Bridge.Ui;

public class ConfigEditorModel
{
    [Required]
    public LoxoneEditorModel Loxone { get; set; } = new();

    [Required]
    public MqttEditorModel Mqtt { get; set; } = new();

    public List<MappingEditorModel> Mappings { get; set; } = new();
}

public class LoxoneEditorModel
{
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 80;

    public bool UseHttps { get; set; }

    [Required]
    public string User { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

}

public class MqttEditorModel
{
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string ClientId { get; set; } = "loxone-z2m-bridge";

    public string BaseTopic { get; set; } = "zigbee2mqtt";
}

public class MappingEditorModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string MqttTopic { get; set; } = string.Empty;

    [Required]
    public string LoxoneUuidAction { get; set; } = string.Empty;
}
