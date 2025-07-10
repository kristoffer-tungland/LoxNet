# LoxNet

C# library for interacting with a Loxone Miniserver. The implementation follows the official communication specification.

```csharp
using LoxNet;

var options = new LoxoneConnectionOptions("192.168.1.77", 443, secure: true);
var client = new LoxoneClient(options);
await client.LoginAsync("admin", "password");
await client.WebSocket.KeepAliveAsync();
await client.WebSocket.CloseAsync();
```

For manual usage without DI:

```csharp
var client = new LoxoneClient(new LoxoneConnectionOptions("192.168.1.77", 443, secure: true));
await client.LoginAsync("admin", "password");
```

## Structure Cache

Load the structure and query controls by name, room or category:

```csharp
var cache = new LoxoneStructureState(client.Http);
await cache.LoadAsync();

if (cache.TryGetControlByName("Kitchen Temp", out var ctrl))
    Console.WriteLine($"UUID: {ctrl.Uuid}, Type: {ctrl.Type}");

foreach (var c in cache.GetControlsByRoom("Bathroom"))
    Console.WriteLine($"{c.Name} ({c.Type})");

foreach (var c in cache.GetControlsByCategory("Lighting"))
    Console.WriteLine($"{c.Name} ({c.Uuid})");
```

## Login and token refresh

Use `LoginAsync` to authenticate and establish the websocket connection.
`LoxoneClient` tracks the token validity and refreshes when needed:

```csharp
await client.LoginAsync("admin", "password");

// call before sending commands to ensure token validity
await client.EnsureValidTokenAsync();
```

