# LoxNet

C# library for interacting with a Loxone Miniserver. The implementation follows the official communication specification.

```csharp
using LoxNet;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHttpClient();
var provider = services.BuildServiceProvider();

var client = new LoxoneClient("192.168.1.77", 443, secure: true);
var jwt = await client.Http.GetJwtAsync("admin", "password", 4, "Example client");
await client.WebSocket.ConnectAsync();
await client.WebSocket.AuthenticateWithTokenAsync(jwt.Token, "admin");
await client.WebSocket.KeepAliveAsync();
await client.DisposeAsync();
```

For manual usage without DI:

```csharp
var client = new LoxoneClient("192.168.1.77", 443, secure: true);
await client.WebSocket.ConnectAsync();
// ...
```
