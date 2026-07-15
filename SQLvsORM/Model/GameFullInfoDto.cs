

namespace SQLvsORM.Model
{
    public class GameFullInfoDto
    {
        public int game_id { get; set; }
        public string title { get; set; }
        public DateTime? release_date { get; set; }
        public string developer { get; set; }
        public string publisher { get; set; }
        public string description { get; set; }
        public decimal? base_price { get; set; }

        public Dictionary<string, string> TextAttributes { get; set; } = new();
        public Dictionary<string, decimal> NumberAttributes { get; set; } = new();
        public Dictionary<string, bool> BooleanAttributes { get; set; } = new();
        public Dictionary<string, DateTime> DateAttributes { get; set; } = new();
    }
}
