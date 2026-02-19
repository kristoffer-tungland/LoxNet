namespace LoxNet;

/// <summary>
/// Represents a Loxone JWT token with its metadata.
/// </summary>
/// <param name="Token">The JWT token string.</param>
/// <param name="ValidUntil">Token expiry time in seconds since 2009-01-01 00:00:00 UTC (Loxone epoch).</param>
/// <param name="TokenRights">Bitmap of granted permissions.</param>
/// <param name="UnsecurePass">Indicates if a weak password is in use.</param>
/// <param name="Key">Key for subsequent commands.</param>
public record TokenInfo(string Token, long ValidUntil, int TokenRights, bool UnsecurePass, string Key)
{
    /// <summary>
    /// Loxone epoch start: 2009-01-01 00:00:00 UTC.
    /// </summary>
    public static readonly DateTimeOffset LoxoneEpoch = new DateTimeOffset(2009, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Converts the ValidUntil timestamp to a DateTimeOffset.
    /// </summary>
    public DateTimeOffset GetExpiryDate() => LoxoneEpoch.AddSeconds(ValidUntil);
}
