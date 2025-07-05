# LoxNet

C# library for interacting with a Loxone Miniserver. The implementation follows the official communication specification.

```csharp
using LoxNet;

var options = new LoxoneConnectionOptions("192.168.1.77", 443, secure: true);
var client = new LoxoneClient(options);
var jwt = await client.Http.GetJwtAsync("admin", "password", 4, "Example client");
await client.WebSocket.ConnectAndAuthenticateAsync("admin");
await client.WebSocket.KeepAliveAsync();
await client.WebSocket.CloseAsync();
```

For manual usage without DI:

```csharp
var client = new LoxoneClient(new LoxoneConnectionOptions("192.168.1.77", 443, secure: true));
var jwt = await client.Http.GetJwtAsync("admin", "password", 4, "Example client");
await client.WebSocket.ConnectAndAuthenticateAsync("admin");
```

## Structure Cache

Load the structure and query controls by name, room or category:

```csharp
var cache = new LoxoneStructureCache(client.Http);
await cache.LoadAsync();

if (cache.TryGetControlByName("Kitchen Temp", out var ctrl))
    Console.WriteLine($"UUID: {ctrl.Uuid}, Type: {ctrl.Type}");

foreach (var c in cache.GetControlsByRoom("Bathroom"))
    Console.WriteLine($"{c.Name} ({c.Type})");

foreach (var c in cache.GetControlsByCategory("Lighting"))
    Console.WriteLine($"{c.Name} ({c.Uuid})");
```

