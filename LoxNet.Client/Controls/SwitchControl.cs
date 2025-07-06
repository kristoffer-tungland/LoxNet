using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Provides convenience methods for the <c>Switch</c> control.
/// </summary>
public class SwitchControl : LoxoneControl<SwitchControlDetails>
{

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
