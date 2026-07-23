namespace SQLvsORM.Models;

public class GameDto
{
    public int game_id { get; set; }
    public string title { get; set; } = "";
    public Dictionary<string, object> properties { get; set; } = new();
}