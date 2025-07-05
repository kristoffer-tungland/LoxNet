namespace LoxNet;

public record LoxoneConnectionOptions(string Host, int Port = 80, bool Secure = false);
