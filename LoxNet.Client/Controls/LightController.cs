using System.Text.Json.Serialization;
using System.Threading;

namespace LoxNet;

/// <summary>
/// Provides methods specific to the <c>LightController</c> type.
/// </summary>
public class LightController : LoxoneControl<LightControllerDetails>
{

    /// <summary>
    /// UUID of the state representing the currently active scene.
    /// </summary>
    [JsonIgnore]
    public string? ActiveSceneState => GetState("activeScene");

    /// <summary>
    /// UUID of the state providing the scene list.
    /// </summary>
    [JsonIgnore]
    public string? SceneListState => GetState("sceneList");

    /// <summary>
    /// Turns the light controller on.
    /// </summary>
    public async Task<LoxoneMessage> SwitchOnAsync(ILoxoneHttpClient client, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, "on", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the light controller off.
    /// </summary>
    public async Task<LoxoneMessage> SwitchOffAsync(ILoxoneHttpClient client, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, "off", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Activates the specified lighting scene.
    /// </summary>
    /// <param name="client">HTTP client used for the command.</param>
    /// <param name="scene">Scene number to activate.</param>
    public async Task<LoxoneMessage> SetSceneAsync(ILoxoneHttpClient client, int scene, CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(client, $"scene/{scene}", cancellationToken).ConfigureAwait(false);
    }
}
