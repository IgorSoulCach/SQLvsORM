using System.Data;
using Npgsql;
using SQLvsORM.Model;
using SQLvsORM.Models;

namespace SQLvsORM.Services;

public class SearchServiceSQL
{
    private readonly string _connectionString;

    public SearchServiceSQL(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ServiceResult<List<GameDto>> Search(string attributeName, string attributeValue, string attributeValue2, SearchType searchType)
    {
        try
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

            var games = new List<GameDto>();
            foreach (DataRow row in dt.Rows)
            {
                games.Add(new GameDto
                {
                    game_id = Convert.ToInt32(row["game_id"]),
                    title = row["title"].ToString(),
                    release_date = row["release_date"] is DBNull ? string.Empty : Convert.ToDateTime(row["release_date"]).ToString("yyyy-MM-dd"),
                    developer = row["developer"].ToString(),
                    publisher = row["publisher"].ToString(),
                    base_price = row["base_price"] is DBNull ? 0 : Convert.ToDecimal(row["base_price"]),
                    attribute_value = GetAttributeValue(row["game_id"].ToString(), attributeName)
                });
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    public ServiceResult<List<GameDto>> GetAllGames()
    {
        try
        {
            var query = "SELECT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g ORDER BY g.title";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();
            var dt = new DataTable();
            dt.Load(reader);

            var games = new List<GameDto>();
            foreach (DataRow row in dt.Rows)
            {
                games.Add(new GameDto
                {
                    game_id = Convert.ToInt32(row["game_id"]),
                    title = row["title"].ToString(),
                    release_date = row["release_date"] is DBNull ? string.Empty : Convert.ToDateTime(row["release_date"]).ToString("yyyy-MM-dd"),
                    developer = row["developer"].ToString(),
                    publisher = row["publisher"].ToString(),
                    base_price = row["base_price"] is DBNull ? 0 : Convert.ToDecimal(row["base_price"])
                });
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    public ServiceResult<GameDto> GetGameById(int id)
    {
        try
        {
            var query = "SELECT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g WHERE g.game_id = @id";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            var dt = new DataTable();
            dt.Load(reader);

            if (dt.Rows.Count == 0)
                return ServiceResult<GameDto>.Fail($"Игра с ID {id} не найдена");

            var row = dt.Rows[0];
            var game = new GameDto
            {
                game_id = Convert.ToInt32(row["game_id"]),
                title = row["title"].ToString(),
                release_date = row["release_date"] is DBNull ? string.Empty : Convert.ToDateTime(row["release_date"]).ToString("yyyy-MM-dd"),
                developer = row["developer"].ToString(),
                publisher = row["publisher"].ToString(),
                base_price = row["base_price"] is DBNull ? 0 : Convert.ToDecimal(row["base_price"])
            };

            return ServiceResult<GameDto>.Success(game);
        }
        catch (Exception ex)
        {
            return ServiceResult<GameDto>.Fail(ex.Message);
        }
    }

    public Dictionary<string, List<string>> GetAllAttributes()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        return new Dictionary<string, List<string>>
        {
            ["text"] = GetDistinctAttributeNames(conn, "AttributeText"),
            ["number"] = GetDistinctAttributeNames(conn, "AttributeNumber"),
            ["boolean"] = GetDistinctAttributeNames(conn, "AttributeBoolean"),
            ["date"] = GetDistinctAttributeNames(conn, "AttributeDate")
        };
    }

    public string GetAttributeValue(string gameId, string attributeName)
    {
        try
        {
            string query = @"
                SELECT attribute_value::TEXT FROM AttributeText WHERE game_id = @gameId AND attribute_name = @attrName
                UNION ALL
                SELECT attribute_value::TEXT FROM AttributeNumber WHERE game_id = @gameId AND attribute_name = @attrName
                UNION ALL
                SELECT attribute_value::TEXT FROM AttributeBoolean WHERE game_id = @gameId AND attribute_name = @attrName
                UNION ALL
                SELECT attribute_value::TEXT FROM AttributeDate WHERE game_id = @gameId AND attribute_name = @attrName
                LIMIT 1";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@gameId", int.Parse(gameId));
            cmd.Parameters.AddWithValue("@attrName", attributeName);

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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

            SearchType.GreaterOrEqual => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value >= CAST(@attrValue AS DECIMAL)",

            SearchType.LessOrEqual => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value <= CAST(@attrValue AS DECIMAL)",

            SearchType.Between => @"
                INNER JOIN AttributeNumber at_num ON g.game_id = at_num.game_id AND at_num.attribute_name = @attrName
                WHERE at_num.attribute_value BETWEEN CAST(@attrValue AS DECIMAL) AND CAST(@attrValue2 AS DECIMAL)",

            SearchType.Contains => @"
                INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                WHERE at_text.attribute_value LIKE '%' || @attrValue || '%'",

            SearchType.In => @"
                INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                WHERE at_text.attribute_value = ANY(STRING_TO_ARRAY(@attrValue, ','))",

            SearchType.NotIn => @"
                INNER JOIN AttributeText at_text ON g.game_id = at_text.game_id AND at_text.attribute_name = @attrName
                WHERE at_text.attribute_value != ALL(STRING_TO_ARRAY(@attrValue, ','))",

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

    private List<string> GetDistinctAttributeNames(NpgsqlConnection conn, string tableName)
    {
        using var cmd = new NpgsqlCommand($"SELECT DISTINCT attribute_name FROM {tableName} ORDER BY attribute_name", conn);
        using var reader = cmd.ExecuteReader();

        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));

        return names;
    }
}