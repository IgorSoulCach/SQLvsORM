namespace SQLvsORM.Models;

public class GameDto 
{
    public int game_id { get; set; }
    public string title { get; set; } = string.Empty;
    public string release_date { get; set; } = string.Empty;
    public string developer { get; set; } = string.Empty;
    public string publisher { get; set; } = string.Empty;
    public decimal base_price { get; set; }
    public string attribute_value { get; set; } = string.Empty;
}