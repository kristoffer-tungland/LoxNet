using System.Text.Json.Serialization;

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
    public async Task<LoxoneMessage> SwitchOnAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "on");
    }

    /// <summary>
    /// Turns the light controller off.
    /// </summary>
    public async Task<LoxoneMessage> SwitchOffAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "off");
    }

    /// <summary>
    /// Activates the specified lighting scene.
    /// </summary>
    /// <param name="client">HTTP client used for the command.</param>
    /// <param name="scene">Scene number to activate.</param>
    public async Task<LoxoneMessage> SetSceneAsync(ILoxoneHttpClient client, int scene)
    {
        return await ExecuteCommandAsync(client, $"scene/{scene}");
    }
}
