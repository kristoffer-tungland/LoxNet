using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoxNet;

public interface ILoxoneStructureCache
{
    IReadOnlyDictionary<string, LoxoneControl> Controls { get; }
    IReadOnlyDictionary<string, LoxoneRoom> Rooms { get; }
    IReadOnlyDictionary<string, LoxoneCategory> Categories { get; }

    Task LoadAsync();
    bool TryGetControl(string uuid, out LoxoneControl? control);
    bool TryGetControlByName(string name, out LoxoneControl? control);
    IEnumerable<LoxoneControl> GetControlsByRoom(string roomName);
    IEnumerable<LoxoneControl> GetControlsByCategory(string categoryName);
}
