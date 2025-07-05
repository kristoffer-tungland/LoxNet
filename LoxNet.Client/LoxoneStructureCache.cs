using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoxNet;

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
                var val = roomProp.Value;
                var name = val.GetProperty("name").GetString() ?? id;
                var image = val.TryGetProperty("image", out var imgVal) ? imgVal.GetString() : null;
                int? rating = null;
                if (val.TryGetProperty("defaultRating", out var ratVal) && ratVal.TryGetInt32(out var r))
                    rating = r;
                _roomMap[id] = new LoxoneRoom(id, name, image, rating);
            }
        }

        if (root.TryGetProperty("cats", out var cats))
        {
            foreach (var catProp in cats.EnumerateObject())
            {
                var id = catProp.Name;
                var val = catProp.Value;
                var name = val.GetProperty("name").GetString() ?? id;
                var type = val.TryGetProperty("type", out var typeVal) ? typeVal.GetString() : null;
                var color = val.TryGetProperty("color", out var colorVal) ? colorVal.GetString() : null;
                _categoryMap[id] = new LoxoneCategory(id, name, type, color);
            }
        }

        if (root.TryGetProperty("controls", out var controls))
        {
            foreach (var controlProp in controls.EnumerateObject())
            {
                AddControl(controlProp.Name, controlProp.Value, null, null);
            }
        }
    }

    private void AddControl(string uuid, JsonElement obj, string? parentRoomId, string? parentCatId)
    {
        var roomId = obj.TryGetProperty("room", out var roomVal) ? roomVal.GetString() : parentRoomId;
        var catId = obj.TryGetProperty("cat", out var catVal) ? catVal.GetString() : parentCatId;

        var control = new LoxoneControl
        {
            Uuid = uuid,
            Name = obj.GetProperty("name").GetString() ?? uuid,
            Type = obj.GetProperty("type").GetString() ?? string.Empty,
            RoomId = roomId,
            CategoryId = catId,
            RoomName = roomId != null && _roomMap.TryGetValue(roomId!, out var room) ? room.Name : null,
            CategoryName = catId != null && _categoryMap.TryGetValue(catId!, out var cat) ? cat.Name : null,
            DefaultRating = obj.TryGetProperty("defaultRating", out var ratingVal) && ratingVal.TryGetInt32(out var r) ? r : (int?)null,
            IsSecured = obj.TryGetProperty("isSecured", out var secVal) ? secVal.GetBoolean() : (bool?)null
        };

        _uuidMap[uuid] = control;

        if (obj.TryGetProperty("uuidAction", out var uuidAction) && uuidAction.GetString() is { } actionUuid && actionUuid != uuid)
        {
            _uuidMap[actionUuid] = control;
        }

        if (obj.TryGetProperty("states", out var states))
        {
            foreach (var stateProp in states.EnumerateObject())
            {
                if (stateProp.Value.GetString() is { } stateUuid)
                    _uuidMap[stateUuid] = control;
            }
        }

        if (obj.TryGetProperty("subControls", out var subControls))
        {
            foreach (var subProp in subControls.EnumerateObject())
            {
                AddControl(subProp.Name, subProp.Value, roomId, catId);
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

