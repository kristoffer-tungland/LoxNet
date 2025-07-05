namespace LoxNet;

using System.Text.Json;

public record LoxoneMessage(int Code, JsonElement? Value, string? Message);

public record KeyInfo(string Key, string Salt, string HashAlg);

public record TokenInfo(string Token, long ValidUntil, int TokenRights, bool UnsecurePass, string Key);

