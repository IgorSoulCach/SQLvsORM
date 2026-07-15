using System.Data;
using Npgsql;

namespace SQLvsORM.Services;

public enum SearchType
{
    Equals = 1,
    NotEquals = 2,
    GreaterThan = 3,
    LessThan = 4,
    Between = 5,
    Contains = 6,
    In = 7,
    Before = 8,
    After = 9
}

public class SearchService
{
    private readonly string _connectionString;

    public SearchService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DataTable Search(string attributeName, string attributeValue, string attributeValue2, SearchType searchType)
    {
        string query = BuildQuery(searchType);

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.AddWithValue("@attrName", attributeName);
        cmd.Parameters.AddWithValue("@attrValue", attributeValue);

        if (searchType == SearchType.Between)
            cmd.Parameters.AddWithValue("@attrValue2", attributeValue2);

        using var reader = cmd.ExecuteReader();
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    private static string BuildQuery(SearchType searchType)
    {
        string selectClause = "SELECT DISTINCT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g";

        string whereClause = searchType switch
        {
            SearchType.Equals => @"
                LEFT JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                LEFT JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                LEFT JOIN AttributeBoolean at_bool ON g.game_id = at_bool.game_id AND at_bool.attribute_name = @attrName
                LEFT JOIN AttributeDate at_date ON g.game_id = at_date.game_id AND at_date.attribute_name = @attrName
                WHERE at_text.attribute_value = @attrValue 
                   OR CAST(at_num.attribute_value AS TEXT) = @attrValue 
                   OR CAST(at_bool.attribute_value AS TEXT) = @attrValue 
                   OR CAST(at_date.attribute_value AS TEXT) = @attrValue",

            SearchType.NotEquals => @"
                LEFT JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                LEFT JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                LEFT JOIN AttributeBoolean at_bool ON g.game_id = at_bool.game_id AND at_bool.attribute_name = @attrName
                LEFT JOIN AttributeDate at_date ON g.game_id = at_date.game_id AND at_date.attribute_name = @attrName
                WHERE at_text.attribute_value != @attrValue 
                   OR CAST(at_num.attribute_value AS TEXT) != @attrValue 
                   OR CAST(at_bool.attribute_value AS TEXT) != @attrValue 
                   OR CAST(at_date.attribute_value AS TEXT) != @attrValue",

            SearchType.GreaterThan => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value > CAST(@attrValue AS DECIMAL)",

            SearchType.LessThan => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value < CAST(@attrValue AS DECIMAL)",

            SearchType.Between => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value BETWEEN CAST(@attrValue AS DECIMAL) AND CAST(@attrValue2 AS DECIMAL)",

            SearchType.Contains => @"
                INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                WHERE at_text.attribute_value LIKE '%' || @attrValue || '%'",

            SearchType.In => @"
                INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                WHERE at_text.attribute_value = ANY(STRING_TO_ARRAY(@attrValue, ','))",

            SearchType.Before => @"
                INNER JOIN AttributeDate at_date ON g.game_id = at_date.game_id AND at_date.attribute_name = @attrName
                WHERE at_date.attribute_value < CAST(@attrValue AS DATE)",

            SearchType.After => @"
                INNER JOIN AttributeDate at_date ON g.game_id = at_date.game_id AND at_date.attribute_name = @attrName
                WHERE at_date.attribute_value > CAST(@attrValue AS DATE)",

            _ => throw new ArgumentException("Неизвестный тип поиска")
        };

        return $"{selectClause} {whereClause} ORDER BY g.title";
    }
}
