using LoxNet;
using LoxNet.Bridge.Config;

namespace LoxNet.Bridge.Loxone;

public interface ILoxoneCommandExecutor
{
    Task SendCommandsAsync(MappingSection mapping, IEnumerable<string> commands, CancellationToken cancellationToken);
    bool TryGetControlType(string uuidAction, out ControlType controlType);
}
