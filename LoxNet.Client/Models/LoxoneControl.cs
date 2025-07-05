namespace LoxNet;

public class LoxoneControl
{
    public string Uuid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string? RoomId { get; init; }
    public string? CategoryId { get; init; }
    public string? RoomName { get; init; }
    public string? CategoryName { get; init; }
    public int? DefaultRating { get; init; }
    public bool? IsSecured { get; init; }
}
