using System.ComponentModel.DataAnnotations.Schema;


namespace SQLvsORM.Model
{
    [Table("attributenumber")]
    public class AttributeNumber
    {
        public int game_id { get; set; }
        public string attribute_name { get; set; }
        public decimal attribute_value { get; set; }

        [ForeignKey("game_id")]
        public Game Game { get; set; }
    }
}
