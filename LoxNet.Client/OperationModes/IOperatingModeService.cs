using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LoxNet;

namespace LoxNet.OperationModes;

/// <summary>
/// Provides access to the operating mode schedule endpoints.
/// </summary>
public interface IOperatingModeService
{
    /// <summary>Retrieves all schedule entries.</summary>
    Task<IReadOnlyList<OperatingModeEntry>> GetEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new schedule entry.</summary>
    Task CreateEntryAsync(string name, string operatingMode, ICalendarModeOption mode, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing schedule entry.</summary>
    Task UpdateEntryAsync(string uuid, string name, string operatingMode, ICalendarModeOption mode, CancellationToken cancellationToken = default);

    /// <summary>Deletes an entry by its UUID.</summary>
    Task DeleteEntryAsync(string uuid, CancellationToken cancellationToken = default);

    /// <summary>Gets the configured heating period string.</summary>
    Task<string> GetHeatPeriodAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the configured cooling period string.</summary>
    Task<string> GetCoolPeriodAsync(CancellationToken cancellationToken = default);
}
