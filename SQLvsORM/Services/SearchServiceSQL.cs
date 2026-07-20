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

    public ServiceResult<List<GameDto>> Search(SearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.AttributeName) && string.IsNullOrWhiteSpace(query.AttributeValue))
            return GetAllGames();

        if (!string.IsNullOrWhiteSpace(query.AttributeName) && string.IsNullOrWhiteSpace(query.AttributeValue))
            return GetGamesWithAttribute(query.AttributeName);

        return SearchByAttribute(query);
    }

    private ServiceResult<List<GameDto>> GetGamesWithAttribute(string attributeName)
    {
        try
        {
            string tableName = FindAttributeTable(attributeName);
            if (tableName == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{attributeName}' не найден");

            string sql = $@"
                SELECT DISTINCT g.game_id, g.title
                FROM Game g
                INNER JOIN {tableName} attr ON g.game_id = attr.game_id AND attr.attribute_name = @attrName
                ORDER BY g.title";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attrName", attributeName);

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

    private ServiceResult<List<GameDto>> SearchByAttribute(SearchQuery query)
    {
        try
        {
            string tableName = FindAttributeTable(query.AttributeName);
            if (tableName == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{query.AttributeName}' не найден");

            string sql = BuildQuery(query, tableName);

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attrName", query.AttributeName);
            cmd.Parameters.AddWithValue("@attrValue", query.AttributeValue ?? "");
            if (query.SearchType == SearchType.Between)
                cmd.Parameters.AddWithValue("@attrValue2", query.AttributeValue2 ?? "");

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
                    attribute_value = GetAttributeValue(row["game_id"].ToString(), query.AttributeName)
                });
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private string? FindAttributeTable(string attributeName)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var tables = new[] { "AttributeText", "AttributeNumber", "AttributeBoolean", "AttributeDate" };
        foreach (var table in tables)
        {
            using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table} WHERE attribute_name = @name LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@name", attributeName);
            if ((long)cmd.ExecuteScalar() > 0)
                return table;
        }
        return null;
    }

    private string BuildQuery(SearchQuery query, string tableName)
    {
        string selectClause = "SELECT DISTINCT g.game_id, g.title FROM Game g";
        string joinClause = $"INNER JOIN {tableName} attr ON g.game_id = attr.game_id AND attr.attribute_name = @attrName";

        string whereClause = query.SearchType switch
        {
            SearchType.Equals => tableName switch
            {
                "AttributeBoolean" => "attr.attribute_value = CAST(@attrValue AS BOOLEAN)",
                "AttributeNumber" => "CAST(attr.attribute_value AS TEXT) = @attrValue",
                "AttributeDate" => "CAST(attr.attribute_value AS TEXT) = @attrValue",
                _ => "attr.attribute_value = @attrValue"
            },

            SearchType.NotEquals => tableName switch
            {
                "AttributeBoolean" => "attr.attribute_value != CAST(@attrValue AS BOOLEAN)",
                "AttributeNumber" => "CAST(attr.attribute_value AS TEXT) != @attrValue",
                "AttributeDate" => "CAST(attr.attribute_value AS TEXT) != @attrValue",
                _ => "attr.attribute_value != @attrValue"
            },

            SearchType.GreaterThan => "attr.attribute_value > CAST(@attrValue AS DECIMAL)",
            SearchType.LessThan => "attr.attribute_value < CAST(@attrValue AS DECIMAL)",
            SearchType.GreaterOrEqual => "attr.attribute_value >= CAST(@attrValue AS DECIMAL)",
            SearchType.LessOrEqual => "attr.attribute_value <= CAST(@attrValue AS DECIMAL)",
            SearchType.Between => "attr.attribute_value BETWEEN CAST(@attrValue AS DECIMAL) AND CAST(@attrValue2 AS DECIMAL)",

            SearchType.Contains => "attr.attribute_value LIKE '%' || @attrValue || '%'",
            SearchType.In => "attr.attribute_value = ANY(STRING_TO_ARRAY(@attrValue, ','))",
            SearchType.NotIn => "attr.attribute_value != ALL(STRING_TO_ARRAY(@attrValue, ','))",

            SearchType.Before => "attr.attribute_value < CAST(@attrValue AS DATE)",
            SearchType.After => "attr.attribute_value > CAST(@attrValue AS DATE)",

            _ => throw new ArgumentException("Неизвестный тип поиска")
        };

        return $"{selectClause} {joinClause} WHERE {whereClause} ORDER BY g.title";
    }

    public ServiceResult<List<GameDto>> GetAllGames()
    {
        try
        {
            var sql = "SELECT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g ORDER BY g.title";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
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
            var sql = "SELECT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g WHERE g.game_id = @id";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
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

    public string GetAttributeValue(string gameId, string attributeName)
    {
        string tableName = FindAttributeTable(attributeName);
        if (tableName == null) return string.Empty;

        string sql = $"SELECT attribute_value::TEXT FROM {tableName} WHERE game_id = @gameId AND attribute_name = @attrName LIMIT 1";

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@gameId", int.Parse(gameId));
        cmd.Parameters.AddWithValue("@attrName", attributeName);

        return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
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