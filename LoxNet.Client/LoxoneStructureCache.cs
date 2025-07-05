using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

public class LoxoneControl
{
    public string Uuid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string? RoomId { get; init; }
    public string? CategoryId { get; init; }
    public string? RoomName { get; init; }
    public string? CategoryName { get; init; }
}

public class LoxoneStructureCache : ILoxoneStructureCache
{
    private readonly ILoxoneHttpClient _httpClient;
    private readonly Dictionary<string, LoxoneControl> _uuidMap = new();
    private readonly Dictionary<string, LoxoneRoom> _roomMap = new();
    private readonly Dictionary<string, LoxoneCategory> _categoryMap = new();

    public LoxoneStructureCache(ILoxoneHttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public IReadOnlyDictionary<string, LoxoneControl> Controls => _uuidMap;
    public IReadOnlyDictionary<string, LoxoneRoom> Rooms => _roomMap;
    public IReadOnlyDictionary<string, LoxoneCategory> Categories => _categoryMap;

    public async Task LoadAsync()
    {
        using var doc = await _httpClient.RequestJsonAsync("data/LoxApp3.json");

        _uuidMap.Clear();
        _roomMap.Clear();
        _categoryMap.Clear();

        var root = doc.RootElement;

        if (root.TryGetProperty("rooms", out var rooms))
        {
            foreach (var roomProp in rooms.EnumerateObject())
            {
                var id = roomProp.Name;
                var name = roomProp.Value.GetProperty("name").GetString() ?? id;
                _roomMap[id] = new LoxoneRoom(id, name);
            }
        }

        if (root.TryGetProperty("cats", out var cats))
        {
            foreach (var catProp in cats.EnumerateObject())
            {
                var id = catProp.Name;
                var name = catProp.Value.GetProperty("name").GetString() ?? id;
                _categoryMap[id] = new LoxoneCategory(id, name);
            }
        }

        if (root.TryGetProperty("controls", out var controls))
        {
            foreach (var controlProp in controls.EnumerateObject())
            {
                var uuid = controlProp.Name;
                var obj = controlProp.Value;

                var roomId = obj.TryGetProperty("room", out var roomVal) ? roomVal.GetString() : null;
                var catId = obj.TryGetProperty("cat", out var catVal) ? catVal.GetString() : null;

                var control = new LoxoneControl
                {
                    Uuid = uuid,
                    Name = obj.GetProperty("name").GetString() ?? uuid,
                    Type = obj.GetProperty("type").GetString() ?? string.Empty,
                    RoomId = roomId,
                    CategoryId = catId,
                    RoomName = roomId != null && _roomMap.TryGetValue(roomId, out var room) ? room.Name : null,
                    CategoryName = catId != null && _categoryMap.TryGetValue(catId, out var cat) ? cat.Name : null
                };

                _uuidMap[uuid] = control;

                if (obj.TryGetProperty("uuidAction", out var uuidAction) &&
                    uuidAction.GetString() is { } actionUuid && actionUuid != uuid)
                {
                    _uuidMap[actionUuid] = control;
                }

                if (obj.TryGetProperty("states", out var states))
                {
                    foreach (var stateProp in states.EnumerateObject())
                    {
                        var stateUuid = stateProp.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(stateUuid))
                        {
                            _uuidMap[stateUuid] = control;
                        }
                    }
                }
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
}

