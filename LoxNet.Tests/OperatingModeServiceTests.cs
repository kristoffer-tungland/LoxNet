using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using LoxNet;
using LoxNet.OperationModes;

namespace LoxNet.Tests;

public class OperatingModeServiceTests
{
    private class MockHttpClient : ILoxoneHttpClient
    {
        public List<string> Paths { get; } = new();
        public LoxoneConnectionOptions Options => new("localhost", 0, false);
        public TokenInfo? LastToken => null;

        private const string EntriesJson = "{\"LL\": { \"Code\": 200, \"value\": [ { \"uuid\": \"1\", \"name\": \"Entry\", \"operatingMode\": \"Party\", \"calMode\": 0, \"calModeAttr\": \"1/1\" } ] } }";
        private const string OkJson = "{\"LL\": { \"Code\": 200 } }";
        private const string HeatJson = "{\"LL\": { \"Code\": 200, \"value\": \"10-15/04-15\" } }";
        private const string CoolJson = "{\"LL\": { \"Code\": 200, \"value\": \"06-01/09-30\" } }";

        public Task<JsonDocument> RequestJsonAsync(string path)
        {
            Paths.Add(path);
            var json = path switch
            {
                "jdev/sps/calendargetentries" => EntriesJson,
                var p when p.StartsWith("jdev/sps/calendarcreateentry") => OkJson,
                var p when p.StartsWith("jdev/sps/calendarupdateentry") => OkJson,
                var p when p.StartsWith("jdev/sps/calendardeleteentry") => OkJson,
                "jdev/sps/calendargetheatperiod" => HeatJson,
                "jdev/sps/calendargetcoolperiod" => CoolJson,
                _ => throw new System.InvalidOperationException(path)
            };
            return Task.FromResult(JsonDocument.Parse(json));
        }

        public Task<KeyInfo> GetKey2Async(string user) => throw new System.NotImplementedException();
        public Task<TokenInfo> GetJwtAsync(string user, string password, int permission, string info) => throw new System.NotImplementedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetEntries_ReturnsParsedEntries()
    {
        var client = new MockHttpClient();
        var svc = new OperatingModeService(client);

        var entries = await svc.GetEntriesAsync();

        Assert.Single(entries);
        var e = entries[0];
        Assert.Equal("1", e.Uuid);
        Assert.Equal("Entry", e.Name);
        Assert.Equal("Party", e.OperatingMode);
        var option = Assert.IsType<YearlyDateOption>(e.Mode);
        Assert.Equal(CalendarMonth.January, option.Month);
        Assert.Equal(1, option.Day);
        Assert.Contains("jdev/sps/calendargetentries", client.Paths);
    }

    [Fact]
    public async Task Commands_ReturnOk()
    {
        var client = new MockHttpClient();
        var svc = new OperatingModeService(client);

        var mode = new YearlyDateOption(CalendarMonth.January, 1);
        await svc.CreateEntryAsync("Name", "Party", mode);
        await svc.UpdateEntryAsync("1", "Name", "Party", mode);
        await svc.DeleteEntryAsync("1");
        var heat = await svc.GetHeatPeriodAsync();
        var cool = await svc.GetCoolPeriodAsync();

        Assert.Equal("10-15/04-15", heat);
        Assert.Equal("06-01/09-30", cool);
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/calendarcreateentry"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/calendarupdateentry/1"));
        Assert.Contains(client.Paths, p => p.StartsWith("jdev/sps/calendardeleteentry/1"));
        Assert.Contains("jdev/sps/calendargetheatperiod", client.Paths);
        Assert.Contains("jdev/sps/calendargetcoolperiod", client.Paths);
    }

    [Fact]
    public void CalendarModeParser_ParsesWeekday()
    {
        var option = (WeekdayOption)CalendarModeParser.Parse(CalendarMode.Weekday, "1/0/1");
        Assert.Equal(CalendarMonth.January, option.Month);
        Assert.Equal(CalendarWeekday.Monday, option.Weekday);
        Assert.Equal(WeekdayOccurrence.First, option.WeekdayInMonth);
    }
}
