using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LoxNet;

namespace LoxNet.OperationModes;

/// <summary>
/// Default implementation of <see cref="IOperatingModeService"/> using <see cref="ILoxoneHttpClient"/>.
/// </summary>
public class OperatingModeService : IOperatingModeService
{
    private readonly ILoxoneHttpClient _client;

    /// <summary>Creates the service.</summary>
    public OperatingModeService(ILoxoneHttpClient httpClient)
    {
        _client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperatingModeEntry>> GetEntriesAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetentries");
        var arr = doc.RootElement.GetProperty("LL").GetProperty("value");
        var dtos = JsonSerializer.Deserialize<OperatingModeEntryDto[]>(arr.GetRawText())!;
        var list = new List<OperatingModeEntry>(dtos.Length);
        foreach (var dto in dtos)
        {
            list.Add(OperatingModeEntryFactory.FromDto(dto));
        }
        return list;
    }

    /// <inheritdoc />
    public async Task CreateEntryAsync(string name, string operatingMode, ICalendarModeOption mode)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendarcreateentry/{Uri.EscapeDataString(name)}/{operatingMode}/{(int)mode.Mode}/{Uri.EscapeDataString(mode.ToQueryAttribute())}");
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task UpdateEntryAsync(string uuid, string name, string operatingMode, ICalendarModeOption mode)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendarupdateentry/{uuid}/{Uri.EscapeDataString(name)}/{operatingMode}/{(int)mode.Mode}/{Uri.EscapeDataString(mode.ToQueryAttribute())}");
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(string uuid)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendardeleteentry/{uuid}");
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task<string> GetHeatPeriodAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetheatperiod");
        return doc.RootElement.GetProperty("LL").GetProperty("value").GetString()!;
    }

    /// <inheritdoc />
    public async Task<string> GetCoolPeriodAsync()
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetcoolperiod");
        return doc.RootElement.GetProperty("LL").GetProperty("value").GetString()!;
    }

}
