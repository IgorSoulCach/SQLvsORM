using SQLvsORM.Enums;

namespace SQLvsORM.Models;

public class SearchQuery
{
    public string? AttributeName { get; set; }
    public string? AttributeValue { get; set; }
    public SearchType SearchType { get; set; } = SearchType.Equals;
    public string? AttributeValue2 { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
}