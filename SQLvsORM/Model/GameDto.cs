namespace SQLvsORM.Models;

    public class GameDto
    {
        public int game_id { get; set; }
        public string title { get; set; }
        public string? release_date { get; set; }
        public string developer { get; set; }
        public string publisher { get; set; }
        public decimal? base_price { get; set; }
        public string? attribute_value { get; set; }
    }

