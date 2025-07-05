namespace LoxNet;

using System.Text.Json;

public record LoxoneMessage(int Code, JsonElement? Value, string? Message);
