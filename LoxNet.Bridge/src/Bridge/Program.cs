using LoxNet.Bridge;
using LoxNet.Bridge.Api;
using LoxNet.Bridge.Config;
using LoxNet.Bridge.Logging;
using LoxNet.Bridge.Loxone;
using LoxNet.Bridge.Mqtt;
using LoxNet.Bridge.Sync;

var builder = Host.CreateApplicationBuilder(args);
LoggingSetup.Configure(builder.Logging);

var bridgeConfig = await ConfigLoader.LoadAsync(builder.Configuration, builder.Configuration["CONFIG"]);
builder.Services.AddSingleton(bridgeConfig);
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
builder.Services.AddSingleton<LoxoneService>();
builder.Services.AddSingleton<ILoxoneCommandExecutor>(sp => sp.GetRequiredService<LoxoneService>());
builder.Services.AddSingleton<SyncEngine>();
builder.Services.AddHostedService<AppHost>();
builder.Services.AddHostedService<MinimalApiHost>();

var host = builder.Build();

await host.RunAsync();
