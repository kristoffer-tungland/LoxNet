using System.Text.Json.Serialization;
using System.Threading;

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
    public async Task<LoxoneMessage> OnAsync(ILoxoneHttpClient client, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, "on", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the switch off.
    /// </summary>
    /// <param name="client">HTTP client used to send the command.</param>
    public async Task<LoxoneMessage> OffAsync(ILoxoneHttpClient client, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, "off", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pulses the switch.
    /// </summary>
    /// <param name="client">HTTP client used to send the command.</param>
    public async Task<LoxoneMessage> PulseAsync(ILoxoneHttpClient client, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, "pulse", cancellationToken).ConfigureAwait(false);
    }
}
