namespace LoxNet;

using System.Text.Json;

/// <summary>
/// Base representation for controls with typed detail information.
/// </summary>
public class LoxoneControl<TDetails> : LoxoneControl
{
    private TDetails? _details;

    /// <summary>
    /// Additional data parsed from <see cref="LoxoneControl.RawDetails"/> on first access.
    /// </summary>
    public TDetails? Details
    {
        get
        {
            if (_details == null && RawDetails.HasValue)
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _details = JsonSerializer.Deserialize<TDetails>(RawDetails.Value.GetRawText(), opts);
            }
            return _details;
        }
        set => _details = value;
    }
}
