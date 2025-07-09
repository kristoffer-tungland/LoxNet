using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
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
    public async Task<IReadOnlyList<OperatingModeEntry>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetentries", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        var arr = msg.Value;
        var dtos = JsonSerializer.Deserialize<OperatingModeEntryDto[]>(arr.GetRawText())!;
        var list = new List<OperatingModeEntry>(dtos.Length);
        foreach (var dto in dtos)
        {
            list.Add(OperatingModeEntryFactory.FromDto(dto));
        }
        return list;
    }

    /// <inheritdoc />
    public async Task CreateEntryAsync(string name, string operatingMode, ICalendarModeOption mode, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendarcreateentry/{Uri.EscapeDataString(name)}/{operatingMode}/{(int)mode.Mode}/{Uri.EscapeDataString(mode.ToQueryAttribute())}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task UpdateEntryAsync(string uuid, string name, string operatingMode, ICalendarModeOption mode, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendarupdateentry/{uuid}/{Uri.EscapeDataString(name)}/{operatingMode}/{(int)mode.Mode}/{Uri.EscapeDataString(mode.ToQueryAttribute())}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(string uuid, CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync($"jdev/sps/calendardeleteentry/{uuid}", cancellationToken).ConfigureAwait(false);
        LoxoneMessageParser.Parse(doc).EnsureSuccess();
    }

    /// <inheritdoc />
    public async Task<string> GetHeatPeriodAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetheatperiod", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        return msg.Value.GetString()!;
    }

    /// <inheritdoc />
    public async Task<string> GetCoolPeriodAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await _client.RequestJsonAsync("jdev/sps/calendargetcoolperiod", cancellationToken).ConfigureAwait(false);
        var msg = LoxoneMessageParser.Parse(doc);
        msg.EnsureSuccess();
        return msg.Value.GetString()!;
    }

}
