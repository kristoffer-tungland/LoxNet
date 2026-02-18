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
public class LoxoneMessage : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Internal reference to keep the JsonDocument(s) alive while JsonElements reference them.
    /// </summary>
    private readonly JsonDocument? _valueDocument;

    public int Code { get; }
    public JsonElement Value { get; }
    public string? Message { get; }

    public LoxoneMessage(int code, JsonElement value, string? message, JsonDocument? valueDocument = null)
    {
        Code = code;
        Value = value;
        Message = message;
        _valueDocument = valueDocument;
    }

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

    /// <summary>
    /// Disposes the underlying JsonDocument if it exists.
    /// </summary>
    public void Dispose()
    {
        _valueDocument?.Dispose();
    }

    /// <summary>
    /// Disposes the underlying JsonDocument if it exists asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _valueDocument?.Dispose();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the underlying document alive (prevents GC collection).
    /// Called automatically during async operations to ensure the document isn't disposed prematurely.
    /// </summary>
    internal void KeepAlive()
    {
        GC.KeepAlive(_valueDocument);
    }
}
