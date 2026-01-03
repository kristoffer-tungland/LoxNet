using LoxNet.Bridge.Config;

namespace LoxNet.Bridge.Loxone;

public interface ILoxoneCommandExecutor
{
    Task SendCommandsAsync(MappingSection mapping, IEnumerable<string> commands, CancellationToken cancellationToken);
}
