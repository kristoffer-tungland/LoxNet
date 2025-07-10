using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoxNet;

/// <summary>
/// Tracks token validity and refreshes when expired.
/// </summary>
public class TokenRefresher
{
    private readonly ILoxoneClient _client;
    private readonly string _user;
    private readonly TimeSpan _refreshWindow;

    /// <summary>
    /// Delegate that ensures a valid JWT token is available.
    /// </summary>
    public Func<CancellationToken, Task<TokenInfo>> RefreshDelegate { get; }

    /// <summary>Creates the refresher.</summary>
    public TokenRefresher(ILoxoneClient client, string user, TimeSpan? refreshWindow = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _user = user ?? throw new ArgumentNullException(nameof(user));
        _refreshWindow = refreshWindow ?? TimeSpan.FromSeconds(30);
        RefreshDelegate = EnsureValidTokenAsync;
    }

    /// <summary>
    /// Ensures that a valid JWT token is available, refreshing if needed.
    /// </summary>
    public async Task<TokenInfo> EnsureValidTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = _client.Http.LastToken ?? throw new InvalidOperationException("No JWT token available");
        var now = DateTimeOffset.UtcNow;
        var expiry = DateTimeOffset.FromUnixTimeSeconds(token.ValidUntil);
        if (expiry - now <= _refreshWindow)
            token = await _client.Http.RefreshJwtAsync(_client.WebSocket, _user, cancellationToken).ConfigureAwait(false);
        return token;
    }
}
