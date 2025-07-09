using System.Collections.Generic;
using System.Threading.Tasks;
using LoxNet;

namespace LoxNet.OperationModes;

/// <summary>
/// Provides access to the operating mode schedule endpoints.
/// </summary>
public interface IOperatingModeService
{
    /// <summary>Retrieves all schedule entries.</summary>
    Task<IReadOnlyList<OperatingModeEntry>> GetEntriesAsync();

    /// <summary>Creates a new schedule entry.</summary>
    Task CreateEntryAsync(string name, string operatingMode, ICalendarModeOption mode);

    /// <summary>Updates an existing schedule entry.</summary>
    Task UpdateEntryAsync(string uuid, string name, string operatingMode, ICalendarModeOption mode);

    /// <summary>Deletes an entry by its UUID.</summary>
    Task DeleteEntryAsync(string uuid);

    /// <summary>Gets the configured heating period string.</summary>
    Task<string> GetHeatPeriodAsync();

    /// <summary>Gets the configured cooling period string.</summary>
    Task<string> GetCoolPeriodAsync();
}
