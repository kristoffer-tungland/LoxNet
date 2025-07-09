namespace LoxNet;

using System.Text.Json;

/// <summary>
/// Represents a raw response returned by the Miniserver.
/// </summary>
/// <param name="Code">The response code returned by the server.</param>
/// <param name="Value">
/// The value element returned by the server. If the response did not contain a
/// <c>value</c> property this will be an empty <see cref="JsonElement"/> with
/// <see cref="JsonValueKind.Undefined"/>.
/// </param>
/// <param name="Message">Optional message provided by the server.</param>
public record LoxoneMessage(int Code, JsonElement Value, string? Message)
{
    /// <summary>
    /// Throws a <see cref="LoxoneApiException"/> if the message code is not
    /// within the HTTP success range (200-299).
    /// </summary>
    public void EnsureSuccess()
    {
        if (Code < 200 || Code >= 300)
        {
            throw new LoxoneApiException(Code, Message);
        }
    }
}
