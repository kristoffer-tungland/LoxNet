namespace LoxNet;

public record TokenInfo(string Token, long ValidUntil, int TokenRights, bool UnsecurePass, string Key);
