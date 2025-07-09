using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

/// <summary>
/// Provides access to the structure file parsed from the Miniserver.
/// </summary>
public interface ILoxoneStructureState
{
    /// <summary>Map of all controls keyed by UUID.</summary>
    IReadOnlyDictionary<string, LoxoneControl> Controls { get; }

    /// <summary>Map of rooms keyed by identifier.</summary>
    IReadOnlyDictionary<string, LoxoneRoom> Rooms { get; }

    /// <summary>Map of categories keyed by identifier.</summary>
    IReadOnlyDictionary<string, LoxoneCategory> Categories { get; }

    /// <summary>Downloads and parses the <c>LoxApp3.json</c> structure file.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Retrieves a control by its UUID.</summary>
    bool TryGetControl(string uuid, out LoxoneControl? control);

    /// <summary>Retrieves a control by its display name.</summary>
    bool TryGetControlByName(string name, out LoxoneControl? control);

    /// <summary>Returns all controls belonging to the specified room.</summary>
    IEnumerable<LoxoneControl> GetControlsByRoom(string roomName);

    /// <summary>Returns all controls belonging to the specified category.</summary>
    IEnumerable<LoxoneControl> GetControlsByCategory(string categoryName);

    /// <summary>Returns the available operating modes keyed by mode id.</summary>
    IReadOnlyDictionary<int, string> GetOperatingModes();

    /// <summary>
    /// Attaches a WebSocket client to receive live state updates.
    /// </summary>
    /// <param name="client">Client used for receiving updates.</param>
    void AttachWebSocketClient(ILoxoneWebSocketClient client);
}
