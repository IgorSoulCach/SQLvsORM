using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace SQLvsORM.Model
{
    [Table("game")]
    public class Game
    {
        [Key]
        public int game_id { get; set; }
        public string title { get; set; }
        public DateTime? release_date { get; set; }
        public string developer { get; set; }
        public string publisher { get; set; }
        public string? description { get; set; }
        public decimal? base_price { get; set; }

        public ICollection<AttributeText> AttributeTexts { get; set; }
        public ICollection<AttributeNumber> AttributeNumbers { get; set; }
        public ICollection<AttributeBoolean> AttributeBooleans { get; set; }
        public ICollection<AttributeDate> AttributeDates { get; set; }
    }
}
