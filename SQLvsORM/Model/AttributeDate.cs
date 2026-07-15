using System.ComponentModel.DataAnnotations.Schema;

namespace SQLvsORM.Model
{
    [Table("attributedate")]
    public class AttributeDate
    {
        public int game_id { get; set; }
        public string attribute_name { get; set; }
        public DateTime attribute_value { get; set; }

        [ForeignKey("game_id")]
        public Game Game { get; set; }
    }
}
