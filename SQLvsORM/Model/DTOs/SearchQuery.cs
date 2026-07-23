namespace SQLvsORM.Models;

public class SearchQuery
{
    public string AttributeNames { get; set; } = string.Empty;
    public string AttributeValues { get; set; } = string.Empty;
    public string SearchTypes { get; set; } = string.Empty;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
}