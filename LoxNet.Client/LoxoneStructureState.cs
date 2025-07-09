using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneStructureState : ILoxoneStructureState
{
    private readonly ILoxoneHttpClient _httpClient;
    private readonly Dictionary<string, LoxoneControl> _uuidMap = new();
    private readonly Dictionary<string, LoxoneRoom> _roomMap = new();
    private readonly Dictionary<string, LoxoneCategory> _categoryMap = new();
    private readonly Dictionary<int, string> _operatingModes = new();
    private readonly bool _lightMode;
    private ILoxoneWebSocketClient? _wsClient;

    /// <summary>
    /// Initializes the cache.
    /// </summary>
    /// <param name="httpClient">HTTP client for server communication.</param>
    /// <param name="lightMode">When <c>true</c> only <see cref="LoxoneControl"/> instances are created.</param>
    public LoxoneStructureState(ILoxoneHttpClient httpClient, bool lightMode = false, ILoxoneWebSocketClient? wsClient = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _lightMode = lightMode;
        if (wsClient != null)
        {
            AttachWebSocketClient(wsClient);
        }
    }

    /// <summary>
    /// Registers a WebSocket client so that state updates are applied to controls.
    /// </summary>
    /// <param name="client">Client used to receive update messages.</param>
    public void AttachWebSocketClient(ILoxoneWebSocketClient client)
    {
        _wsClient = client ?? throw new ArgumentNullException(nameof(client));
        _wsClient.MessageReceived += HandleWebSocketMessage;
    }

    public IReadOnlyDictionary<string, LoxoneControl> Controls => _uuidMap;
    public IReadOnlyDictionary<string, LoxoneRoom> Rooms => _roomMap;
    public IReadOnlyDictionary<string, LoxoneCategory> Categories => _categoryMap;
    public IReadOnlyDictionary<int, string> GetOperatingModes() => _operatingModes;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _httpClient.RequestJsonAsync("data/LoxApp3.json", cancellationToken).ConfigureAwait(false);

        _uuidMap.Clear();
        _roomMap.Clear();
        _categoryMap.Clear();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var structure = JsonSerializer.Deserialize<StructureFileDto>(doc.RootElement.GetRawText(), options);
        if (structure == null)
            return;

        if (structure.Rooms is { } rooms)
        {
            foreach (var kvp in rooms)
            {
                var dto = kvp.Value;
                _roomMap[kvp.Key] = new LoxoneRoom(kvp.Key, dto.Name, dto.Image, dto.DefaultRating);
            }
        }

        if (structure.Categories is { } cats)
        {
            foreach (var kvp in cats)
            {
                var dto = kvp.Value;
                _categoryMap[kvp.Key] = new LoxoneCategory(kvp.Key, dto.Name, dto.Type, dto.Color);
            }
        }

        _operatingModes.Clear();
        if (structure.OperatingModes is { } modes)
        {
            foreach (var kvp in modes)
            {
                if (int.TryParse(kvp.Key, out var id))
                {
                    _operatingModes[id] = kvp.Value.Name;
                }
            }
        }

        if (structure.Controls is { } ctrls)
        {
            foreach (var kvp in ctrls)
            {
                AddControl(kvp.Key, kvp.Value, null, null, options);
            }
        }
    }

    private void AddControl(string uuid, ControlDto dto, string? parentRoomId, string? parentCatId, JsonSerializerOptions options, LoxoneControl? host = null)
    {
        var roomId = dto.Room ?? parentRoomId;
        var catId = dto.Category ?? parentCatId;

        ControlType ctrlType = dto.Type switch
        {
            "Switch" => ControlType.Switch,
            "LightController" => ControlType.LightController,
            "LightControllerV2" => ControlType.LightControllerV2,
            _ => ControlType.Unknown
        };

        var name = dto.Name ?? uuid;
        var roomName = roomId != null && _roomMap.TryGetValue(roomId, out var roomObj) ? roomObj.Name : null;
        var categoryName = catId != null && _categoryMap.TryGetValue(catId, out var catObj) ? catObj.Name : null;

        var control = _lightMode
            ? new LoxoneControl()
            : LoxoneControlFactory.Create(ctrlType);

        control.Uuid = uuid;
        control.Name = name;
        control.Type = ctrlType;
        control.RoomId = roomId;
        control.CategoryId = catId;
        control.RoomName = roomName;
        control.CategoryName = categoryName;
        control.DefaultRating = dto.DefaultRating;
        control.IsSecured = dto.IsSecured;
        control.SecuredDetails = dto.SecuredDetails;
        control.UuidAction = dto.UuidAction;
        control.RawDetails = dto.Details;
        control.Statistic = dto.Statistic;
        control.Restrictions = dto.Restrictions;
        control.HasControlNotes = dto.HasControlNotes;
        control.Preset = dto.Preset is { } p ? new LoxonePreset(p.Uuid, p.Name) : null;
        control.Links = dto.Links;
        control.States = dto.States;

        if (dto.States is not null)
        {
            foreach (var kvp in dto.States)
            {
                _uuidMap[kvp.Value] = control;
            }
        }

        if (host == null)
        {
            _uuidMap[uuid] = control;

            if (control.UuidAction is { } actionUuid && actionUuid != uuid)
            {
                _uuidMap[actionUuid] = control;
            }
        }
        else
        {
            host.SubControls[uuid] = control;

            if (control.UuidAction is { } actionUuid)
            {
                _uuidMap[actionUuid] = control;
            }
        }

        if (dto.SubControls is { } subs)
        {
            foreach (var sub in subs)
            {
                AddControl(sub.Key, sub.Value, roomId, catId, options, control);
            }
        }
    }

    public bool TryGetControl(string uuid, out LoxoneControl? control) =>
        _uuidMap.TryGetValue(uuid, out control);

    public bool TryGetControlByName(string name, out LoxoneControl? control)
    {
        control = _uuidMap.Values.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        return control is not null;
    }

    public IEnumerable<LoxoneControl> GetControlsByRoom(string roomName)
    {
        return _uuidMap.Values
            .Where(c => string.Equals(c.RoomName, roomName, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<LoxoneControl> GetControlsByCategory(string categoryName)
    {
        return _uuidMap.Values
            .Where(c => string.Equals(c.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase));
    }

    private void HandleWebSocketMessage(object? sender, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("uuid", out var uuidProp) || !root.TryGetProperty("value", out var valueProp))
            return;

        var uuid = uuidProp.GetString();
        if (uuid is null)
            return;

        var value = valueProp.ToString();
        if (_uuidMap.TryGetValue(uuid, out var ctrl) && ctrl.States is not null)
        {
            foreach (var kvp in ctrl.States)
            {
                if (kvp.Value == uuid)
                {
                    ctrl.UpdateStateValue(kvp.Key, value);
                    break;
                }
            }
        }
    }
}

