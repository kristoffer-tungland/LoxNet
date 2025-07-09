namespace LoxNet;

using System.Text.Json;

public record LoxoneMessage(int Code, JsonElement? Value, string? Message)
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
