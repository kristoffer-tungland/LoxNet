using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LoxNet;

/// <summary>
/// Provides commands for the LightControllerV2 control.
/// </summary>
public class LightControllerV2 : LoxoneControl<LightControllerV2Details>
{

    /// <summary>
    /// UUID of the state listing the active moods.
    /// </summary>
    [JsonIgnore]
    public string? ActiveMoodsState => GetState("activeMoods");

    /// <summary>
    /// UUID of the state providing the mood list.
    /// </summary>
    [JsonIgnore]
    public string? MoodListState => GetState("moodList");

    /// <summary>Changes directly to the specified mood.</summary>
    /// <param name="client">HTTP client used for the command.</param>
    /// <param name="moodId">Identifier of the mood.</param>
    public async Task<LoxoneMessage> ChangeToAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"changeTo/{moodId}");
    }

    /// <summary>Adds a mood to the set of active moods.</summary>
    public async Task<LoxoneMessage> AddMoodAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"addMood/{moodId}");
    }

    /// <summary>Removes a mood from the set of active moods.</summary>
    public async Task<LoxoneMessage> RemoveMoodAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"removeMood/{moodId}");
    }

    /// <summary>Moves a favorite mood to a new index.</summary>
    public async Task<LoxoneMessage> MoveFavoriteMoodAsync(ILoxoneHttpClient client, string moodId, int newIndex)
    {
        return await ExecuteCommandAsync(client, $"moveFavoriteMood/{moodId}/{newIndex}");
    }

    /// <summary>Moves an additional mood to a new index.</summary>
    public async Task<LoxoneMessage> MoveAdditionalMoodAsync(ILoxoneHttpClient client, string moodId, int newIndex)
    {
        return await ExecuteCommandAsync(client, $"moveAdditionalMood/{moodId}/{newIndex}");
    }

    /// <summary>Moves a mood in the complete mood list.</summary>
    public async Task<LoxoneMessage> MoveMoodAsync(ILoxoneHttpClient client, string moodId, int newIndex)
    {
        return await ExecuteCommandAsync(client, $"moveMood/{moodId}/{newIndex}");
    }

    /// <summary>Adds the specified mood to the favorites list.</summary>
    public async Task<LoxoneMessage> AddToFavoriteMoodAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"addToFavoriteMood/{moodId}");
    }

    /// <summary>Removes the specified mood from the favorites list.</summary>
    public async Task<LoxoneMessage> RemoveFromFavoriteMoodAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"removeFromFavoriteMood/{moodId}");
    }

    /// <summary>Stores the current values as a new mood.</summary>
    public async Task<LoxoneMessage> LearnAsync(ILoxoneHttpClient client, string moodId, string moodName)
    {
        return await ExecuteCommandAsync(client, $"learn/{moodId}/{Uri.EscapeDataString(moodName)}");
    }

    /// <summary>Deletes the specified mood.</summary>
    public async Task<LoxoneMessage> DeleteMoodAsync(ILoxoneHttpClient client, string moodId)
    {
        return await ExecuteCommandAsync(client, $"delete/{moodId}");
    }

    /// <summary>Advances to the next mood.</summary>
    public async Task<LoxoneMessage> PlusAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "plus");
    }

    /// <summary>Switches to the previous mood.</summary>
    public async Task<LoxoneMessage> MinusAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "minus");
    }

    /// <summary>Changes the identifier of an existing mood.</summary>
    public async Task<LoxoneMessage> SetMoodIdAsync(ILoxoneHttpClient client, string currentId, string newId)
    {
        return await ExecuteCommandAsync(client, $"setmoodid/{currentId}/{newId}");
    }

    /// <summary>Sets the short name of a mood.</summary>
    public async Task<LoxoneMessage> SetMoodShortNameAsync(ILoxoneHttpClient client, string moodId, string shortName)
    {
        return await ExecuteCommandAsync(client, $"setmoodshortname/{moodId}/{Uri.EscapeDataString(shortName)}");
    }

    /// <summary>Sets the full name of a mood.</summary>
    public async Task<LoxoneMessage> SetMoodNameAsync(ILoxoneHttpClient client, string moodId, string name)
    {
        return await ExecuteCommandAsync(client, $"setmoodname/{moodId}/{Uri.EscapeDataString(name)}");
    }

    /// <summary>Updates circuit names using a JSON payload.</summary>
    public async Task<LoxoneMessage> SetCircuitNamesAsync(ILoxoneHttpClient client, string json)
    {
        return await ExecuteCommandAsync(client, $"setcircuitnames/{Uri.EscapeDataString(json)}");
    }

    /// <summary>Updates the daylight configuration using a JSON payload.</summary>
    public async Task<LoxoneMessage> SetDaylightConfigAsync(ILoxoneHttpClient client, string json)
    {
        return await ExecuteCommandAsync(client, $"setdaylightconfig/{Uri.EscapeDataString(json)}");
    }

    /// <summary>Enables presence functionality.</summary>
    public async Task<LoxoneMessage> PresenceOnAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "presence/on");
    }

    /// <summary>Disables presence functionality.</summary>
    public async Task<LoxoneMessage> PresenceOffAsync(ILoxoneHttpClient client)
    {
        return await ExecuteCommandAsync(client, "presence/off");
    }
}
