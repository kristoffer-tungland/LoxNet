# LoxNet

C# library for interacting with a Loxone Miniserver. The implementation follows the official communication specification.

```csharp
using LoxNet;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHttpClient<LoxoneClient>(c =>
{
    c.BaseAddress = new Uri("https://192.168.1.77:443");
});
var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<LoxoneClient>();
await client.ConnectAsync();
var token = await client.GetJwtAsync("admin", "password", 4, "Example client");
await client.AuthenticateWithTokenAsync(token.Token, "admin");
await client.KeepAliveAsync();
await client.CloseAsync();
```

For manual usage without DI:

```csharp
var client = new LoxoneClient("192.168.1.77", 443, secure: true);
await client.ConnectAsync();
// ...
```
