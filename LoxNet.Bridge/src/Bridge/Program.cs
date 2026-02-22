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
builder.Services.AddSingleton(bridgeConfig);
builder.Services.AddSingleton(new ConfigFileSettings(configPath));
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

// Register bridge as hosted service
builder.Services.AddHostedService<AppHost>();

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
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map API endpoints
app.MapGet("/health", ApiEndpoints.BuildHealth);
app.MapGet("/api/loxone/subcontrols", ApiEndpoints.DiscoverLoxoneSubcontrols);
app.MapGet("/api/mqtt/lights", ApiEndpoints.DiscoverMqttLights);
app.MapPost("/api/loxone/connect", ApiEndpoints.ConnectLoxoneAsync);
app.MapPost("/api/mqtt/connect", ApiEndpoints.ConnectMqttAsync);
app.MapGet("/config", (BridgeConfig config) => Results.Ok(BridgeConfigMapper.ToDto(config)));
app.MapPost("/config", ApiEndpoints.SaveConfigAsync);

await app.RunAsync();
