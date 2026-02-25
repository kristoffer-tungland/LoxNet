using LoxNet;
using LoxNet.Bridge;
using LoxNet.Bridge.Api;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Logging;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;
using LoxNet.Bridge.Ui.Services;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using LoxNet.Bridge.Ui;

// Use WebApplication builder instead of Host builder
var builder = WebApplication.CreateBuilder(args);

var configPath = ConfigLoader.ResolvePath(builder.Configuration, builder.Configuration["CONFIG"]);
var bridgeConfig = await ConfigLoader.LoadAsync(builder.Configuration, configPath);
LoggingSetup.Configure(builder.Logging, builder.Configuration, bridgeConfig.Logging);

// Register bridge services
var configFileSettings = new ConfigFileSettings(configPath);
builder.Services.AddSingleton(new ConfigStore(bridgeConfig, configFileSettings));
builder.Services.AddSingleton(configFileSettings);
builder.Services.AddSingleton<StateCache>();
builder.Services.AddSingleton<StateComparer>();
builder.Services.AddSingleton<Converters>();
builder.Services.AddSingleton<Z2mMessageParser>();
builder.Services.AddSingleton<Z2mPublisher>();
builder.Services.AddSingleton<LoxoneStateParser>();
builder.Services.AddSingleton<LoxoneCommandBuilder>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<IMqttClientHost>(sp => sp.GetRequiredService<MqttService>());
builder.Services.AddSingleton<IMqttPublisher>(sp => new MqttPublisherAdapter(sp.GetRequiredService<IMqttClientHost>(), sp.GetRequiredService<Z2mPublisher>()));
builder.Services.AddSingleton<LoxoneService>(sp =>
    new LoxoneService(
        sp.GetRequiredService<ILogger<LoxoneService>>(),
        sp.GetRequiredService<ILogger<LoxoneClient>>(),
        sp.GetRequiredService<ILogger<LoxoneStructureState>>(),
        sp.GetRequiredService<LoxoneStateParser>()
    )
);
builder.Services.AddSingleton<ILoxoneCommandExecutor>(sp => sp.GetRequiredService<LoxoneService>());
builder.Services.AddSingleton<ConnectionStatusService>();
builder.Services.AddSingleton<SyncEngine>();
// Register in-memory log sink so UI pages can inject and subscribe to it
builder.Services.AddSingleton(LoggingSetup.InMemorySink);

// Register AppHost as a resolvable singleton AND as a hosted service
builder.Services.AddSingleton<AppHost>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AppHost>());

// Register web UI services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient("Default", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});
builder.Services.AddFluentUIComponents();

var app = builder.Build();

LoggingExtensions.SetLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
    app.UseAntiforgery();
}

if (app.Environment.IsDevelopment())
{
    app.UseAntiforgery();
}

app.MapStaticAssets();
var razorComponents = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Disable antiforgery validation for Blazor's circuit negotiation in development.
// Each server restart regenerates Data Protection keys, invalidating the antiforgery
// cookie from the previous session and causing a 403 when reusing the same browser.
if (app.Environment.IsDevelopment())
{
    razorComponents.DisableAntiforgery();
}

// Map API endpoints
app.MapGet("/health", ApiEndpoints.BuildHealth);
app.MapGet("/api/loxone/subcontrols", ApiEndpoints.DiscoverLoxoneSubcontrols);
app.MapGet("/api/mqtt/lights", ApiEndpoints.DiscoverMqttLightsAsync);
app.MapPost("/api/loxone/connect", ApiEndpoints.ConnectLoxoneAsync);
app.MapPost("/api/mqtt/connect", ApiEndpoints.ConnectMqttAsync);
app.MapPost("/api/loxone/settings", ApiEndpoints.SaveLoxoneSettingsAsync);
app.MapPost("/api/mqtt/settings", ApiEndpoints.SaveMqttSettingsAsync);
app.MapPost("/api/sync/settings", ApiEndpoints.SaveSyncSettingsAsync);
app.MapPost("/api/logging/level", ApiEndpoints.SaveLoggingSettingsAsync);
app.MapGet("/config", (ConfigStore configStore) => Results.Ok(BridgeConfigMapper.ToDto(configStore.Current)));
app.MapPost("/config", ApiEndpoints.SaveMappingsAsync);

await app.RunAsync();
