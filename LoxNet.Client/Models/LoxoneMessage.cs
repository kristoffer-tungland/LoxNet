namespace LoxNet;

using System.Text.Json;

public record LoxoneMessage(int Code, JsonElement? Value, string? Message)
{
    /// <summary>
    /// Throws a <see cref="LoxoneApiException"/> if the message code is not 200.
    /// </summary>
    public void EnsureSuccess()
    {
        if (Code != 200)
        {
            throw new LoxoneApiException(Code, Message);
        }
    }
}
