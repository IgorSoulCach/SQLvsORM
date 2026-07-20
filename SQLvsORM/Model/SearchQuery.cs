using SQLvsORM.Services;

namespace SQLvsORM.Models;

public class SearchQuery
{
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeValue { get; set; } = string.Empty;
    public SearchType SearchType { get; set; } = SearchType.Equals;
    public string AttributeValue2 { get; set; } = string.Empty;
}