using System.Data;
using Npgsql;
using SQLvsORM.Enums;
using SQLvsORM.Model.DTOs;
using SQLvsORM.Models;

namespace SQLvsORM.Services;

public class SearchServiceSQL
{
    private readonly string _connectionString;

    public SearchServiceSQL(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ServiceResult<List<GameDto>> Search(SearchQuery query, int skip = 0, int take = 100)
    {
        if (string.IsNullOrWhiteSpace(query.AttributeName) && string.IsNullOrWhiteSpace(query.AttributeValue))
            return GetAllGames(skip, take);

        if (!string.IsNullOrWhiteSpace(query.AttributeName) && string.IsNullOrWhiteSpace(query.AttributeValue))
            return GetGamesWithAttribute(query.AttributeName, skip, take);

        return SearchByAttribute(query, skip, take);
    }

    private ServiceResult<List<GameDto>> GetGamesWithAttribute(string attributeName, int skip, int take)
    {
        try
        {
            string tableName = FindAttributeTable(attributeName);
            if (tableName == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{attributeName}' не найден");

            string sql = $@"
                SELECT DISTINCT g.game_id, g.title, attr.attribute_value::TEXT
                FROM Game g
                INNER JOIN {tableName} attr ON g.game_id = attr.game_id AND attr.attribute_name = @attrName
                ORDER BY g.title
                OFFSET @skip LIMIT @take";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attrName", attributeName);
            cmd.Parameters.AddWithValue("@skip", skip);
            cmd.Parameters.AddWithValue("@take", take);

            using var reader = cmd.ExecuteReader();
            var games = new List<GameDto>();
            while (reader.Read())
            {
                games.Add(new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    attribute_value = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                });
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private ServiceResult<List<GameDto>> SearchByAttribute(SearchQuery query, int skip, int take)
    {
        try
        {
            string tableName = FindAttributeTable(query.AttributeName);
            if (tableName == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{query.AttributeName}' не найден");

            string sql = BuildQuery(query, tableName) + $" OFFSET {skip} LIMIT {take}";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attrName", query.AttributeName);
            cmd.Parameters.AddWithValue("@attrValue", query.AttributeValue ?? "");
            if (query.SearchType == SearchType.Between)
                cmd.Parameters.AddWithValue("@attrValue2", query.AttributeValue2 ?? "");

            using var reader = cmd.ExecuteReader();
            var games = new List<GameDto>();
            while (reader.Read())
            {
                games.Add(new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    attribute_value = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
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

        var tables = new[] { "attributetext", "attributenumber", "attributeboolean", "attributedate" };
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
        string selectClause = "SELECT DISTINCT g.game_id, g.title, attr.attribute_value::TEXT FROM Game g";
        string joinClause = $"INNER JOIN {tableName} attr ON g.game_id = attr.game_id AND attr.attribute_name = @attrName";

        string whereClause = query.SearchType switch
        {
            SearchType.Equals => tableName switch
            {
                "attributeboolean" => "attr.attribute_value = CAST(@attrValue AS BOOLEAN)",
                "attributenumber" => "CAST(attr.attribute_value AS TEXT) = @attrValue",
                "attributedate" => "CAST(attr.attribute_value AS TEXT) = @attrValue",
                _ => "attr.attribute_value = @attrValue"
            },

            SearchType.NotEquals => tableName switch
            {
                "attributeboolean" => "attr.attribute_value != CAST(@attrValue AS BOOLEAN)",
                "attributenumber" => "CAST(attr.attribute_value AS TEXT) != @attrValue",
                "attributedate" => "CAST(attr.attribute_value AS TEXT) != @attrValue",
                _ => "attr.attribute_value != @attrValue"
            },

            SearchType.GreaterThan => tableName switch
            {
                "attributedate" => "attr.attribute_value > CAST(@attrValue AS DATE)",
                _ => "attr.attribute_value > CAST(@attrValue AS DECIMAL)"
            },

            SearchType.LessThan => tableName switch
            {
                "attributedate" => "attr.attribute_value < CAST(@attrValue AS DATE)",
                _ => "attr.attribute_value < CAST(@attrValue AS DECIMAL)"
            },

            SearchType.GreaterOrEqual => tableName switch
            {
                "attributedate" => "attr.attribute_value >= CAST(@attrValue AS DATE)",
                _ => "attr.attribute_value >= CAST(@attrValue AS DECIMAL)"
            },

            SearchType.LessOrEqual => tableName switch
            {
                "attributedate" => "attr.attribute_value <= CAST(@attrValue AS DATE)",
                _ => "attr.attribute_value <= CAST(@attrValue AS DECIMAL)"
            },

            SearchType.Between => tableName switch
            {
                "attributedate" => "attr.attribute_value BETWEEN CAST(@attrValue AS DATE) AND CAST(@attrValue2 AS DATE)",
                _ => "attr.attribute_value BETWEEN CAST(@attrValue AS DECIMAL) AND CAST(@attrValue2 AS DECIMAL)"
            },

            SearchType.Contains => tableName switch
            {
                "attributetext" => "attr.attribute_value = ANY(STRING_TO_ARRAY(@attrValue, ',')) OR attr.attribute_value LIKE '%' || @attrValue || '%'",
                _ => "CAST(attr.attribute_value AS TEXT) = ANY(STRING_TO_ARRAY(@attrValue, ',')) OR CAST(attr.attribute_value AS TEXT) LIKE '%' || @attrValue || '%'"
            },

            SearchType.NotContains => tableName switch
            {
                "attributetext" => "attr.attribute_value != ALL(STRING_TO_ARRAY(@attrValue, ',')) AND attr.attribute_value NOT LIKE '%' || @attrValue || '%'",
                _ => "CAST(attr.attribute_value AS TEXT) != ALL(STRING_TO_ARRAY(@attrValue, ',')) AND CAST(attr.attribute_value AS TEXT) NOT LIKE '%' || @attrValue || '%'"
            },

            _ => throw new ArgumentException("Неизвестный тип поиска")
        };

        return $"{selectClause} {joinClause} WHERE {whereClause} ORDER BY g.title";
    }

    public ServiceResult<List<GameDto>> GetAllGames(int skip = 0, int take = 100)
    {
        try
        {
            var sql = "SELECT g.game_id, g.title, g.release_date, g.developer, g.publisher, g.base_price FROM Game g ORDER BY g.title OFFSET @skip LIMIT @take";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@skip", skip);
            cmd.Parameters.AddWithValue("@take", take);
            using var reader = cmd.ExecuteReader();

            var games = new List<GameDto>();
            while (reader.Read())
            {
                games.Add(new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1),
                    release_date = reader.IsDBNull(2) ? string.Empty : reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                    developer = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    publisher = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    base_price = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
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

            if (!reader.Read())
                return ServiceResult<GameDto>.Fail($"Игра с ID {id} не найдена");

            var game = new GameDto
            {
                game_id = reader.GetInt32(0),
                title = reader.GetString(1),
                release_date = reader.IsDBNull(2) ? string.Empty : reader.GetDateTime(2).ToString("yyyy-MM-dd"),
                developer = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                publisher = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                base_price = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
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
            ["text"] = GetDistinctAttributeNames(conn, "attributetext"),
            ["number"] = GetDistinctAttributeNames(conn, "attributenumber"),
            ["boolean"] = GetDistinctAttributeNames(conn, "attributeboolean"),
            ["date"] = GetDistinctAttributeNames(conn, "attributedate")
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