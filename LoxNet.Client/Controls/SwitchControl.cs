using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Provides convenience methods for the <c>Switch</c> control.
/// </summary>
public class SwitchControl : LoxoneControl
{
    private SwitchControlDetails? _details;

    /// <summary>
    /// Additional detail information for this switch control.
    /// </summary>
    public SwitchControlDetails? Details
    {
        get
        {
            if (_details == null && RawDetails.HasValue)
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _details = JsonSerializer.Deserialize<SwitchControlDetails>(RawDetails.Value.GetRawText(), opts);
            }
            return _details;
        }
        set => _details = value;
    }

    /// <summary>
    /// UUID of the "active" state for this switch.
    /// </summary>
    [JsonIgnore]
    public string? ActiveState => GetState("active");

    /// <summary>
    /// Turns the switch on.
    /// </summary>
    /// <param name="client">HTTP client used to send the command.</param>
    public async Task<LoxoneMessage> OnAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "on");
    }

    /// <summary>
    /// Turns the switch off.
    /// </summary>
    /// <param name="client">HTTP client used to send the command.</param>
    public async Task<LoxoneMessage> OffAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "off");
    }

    /// <summary>
    /// Pulses the switch.
    /// </summary>
    /// <param name="client">HTTP client used to send the command.</param>
    public async Task<LoxoneMessage> PulseAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "pulse");
    }
}
