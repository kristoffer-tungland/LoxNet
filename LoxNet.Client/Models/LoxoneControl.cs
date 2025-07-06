using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Base representation of a Loxone control.
/// </summary>
public class LoxoneControl
{
    /// <summary>Unique identifier of the control.</summary>
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Displayed name of the control.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of the control returned by the Miniserver.</summary>
    [JsonPropertyName("type")]
    public ControlType Type { get; set; }

    /// <summary>Identifier of the room containing the control.</summary>
    [JsonPropertyName("room")]
    public string? RoomId { get; set; }

    /// <summary>Identifier of the category containing the control.</summary>
    [JsonPropertyName("cat")]
    public string? CategoryId { get; set; }

    /// <summary>Name of the room containing the control.</summary>
    public string? RoomName { get; set; }

    /// <summary>Name of the category containing the control.</summary>
    public string? CategoryName { get; set; }

    /// <summary>Default rating value of the control.</summary>
    [JsonPropertyName("defaultRating")]
    public int? DefaultRating { get; set; }

    /// <summary>Indicates whether the control is secured.</summary>
    [JsonPropertyName("isSecured")]
    public bool? IsSecured { get; set; }

    /// <summary>Flag indicating the control contains sensitive information.</summary>
    [JsonPropertyName("securedDetails")]
    public bool? SecuredDetails { get; set; }

    /// <summary>UUID used when sending commands to the control.</summary>
    [JsonPropertyName("uuidAction")]
    public string? UuidAction { get; set; }

    /// <summary>Unparsed details element from the structure file.</summary>
    [JsonPropertyName("details")]
    public JsonElement? RawDetails { get; set; }

    /// <summary>Statistic configuration for this control.</summary>
    [JsonPropertyName("statistic")]
    public JsonElement? Statistic { get; set; }

    /// <summary>Restriction flags for this control.</summary>
    [JsonPropertyName("restrictions")]
    public int? Restrictions { get; set; }

    /// <summary>Indicates if notes are available for this control.</summary>
    [JsonPropertyName("hasControlNotes")]
    public bool? HasControlNotes { get; set; }

    /// <summary>Preset information if the control uses one.</summary>
    [JsonPropertyName("preset")]
    public LoxonePreset? Preset { get; set; }

    /// <summary>UUIDs of linked controls.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<string>? Links { get; set; }

    /// <summary>
    /// Collection of sub controls belonging to this control.
    /// </summary>
    public Dictionary<string, LoxoneControl> SubControls { get; } = new();

    /// <summary>Mapping of state names to UUIDs.</summary>
    [JsonPropertyName("states")]
    public IReadOnlyDictionary<string, string>? States { get; set; }

    /// <summary>
    /// Current values of states keyed by their name.
    /// </summary>
    public Dictionary<string, string> StateValues { get; } = new();

    /// <summary>
    /// Event raised when a state value changes.
    /// </summary>
    public event EventHandler<LoxoneStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Retrieves a state UUID by name if present.
    /// </summary>
    /// <param name="name">Name of the state.</param>
    protected string? GetState(string name)
    {
        return States != null && States.TryGetValue(name, out var uuid) ? uuid : null;
    }

    internal void UpdateStateValue(string name, string value)
    {
        StateValues[name] = value;
        StateChanged?.Invoke(this, new LoxoneStateChangedEventArgs(name, value));
    }

    /// <summary>
    /// Executes an SPS command on this control using the provided HTTP client.
    /// </summary>
    /// <param name="client">HTTP client used to communicate with the Miniserver.</param>
    /// <param name="command">Command path to append after the action UUID.</param>
    protected async Task<LoxoneMessage> ExecuteCommandAsync(ILoxoneHttpClient client, string command)
    {
        using var doc = await client.RequestJsonAsync($"dev/sps/io/{UuidAction}/{command}");
        var ll = doc.RootElement.GetProperty("LL");
        JsonElement? value = ll.TryGetProperty("value", out var v) ? v : (JsonElement?)null;
        string? message = ll.TryGetProperty("message", out var msg) ? msg.GetString() : null;
        return new LoxoneMessage(ll.GetProperty("Code").GetInt32(), value, message);
    }
}
