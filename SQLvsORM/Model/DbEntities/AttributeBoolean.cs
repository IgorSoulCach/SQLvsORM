using System.ComponentModel.DataAnnotations.Schema;


namespace SQLvsORM.Model.DbEntities
{
    [Table("attributeboolean")]
    public class AttributeBoolean
    {
        public int game_id { get; set; }
        public string attribute_name { get; set; }
        public bool attribute_value { get; set; }

        [ForeignKey("game_id")]
        public Game Game { get; set; }
    }
}
