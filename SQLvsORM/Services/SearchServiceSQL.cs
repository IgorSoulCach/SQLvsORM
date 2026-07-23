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

    public ServiceResult<List<GameDto>> Search(SearchQuery query)
    {
        var names = query.AttributeNames.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToArray();

        if (names.Length == 0)
            return GetAllGames(query.Skip, query.Take);

        var values = query.AttributeValues.Split(',').Select(v => v.Trim()).ToArray();
        var types = query.SearchTypes.Split(',').Select(t => int.TryParse(t.Trim(), out var tp) ? (SearchType)tp : SearchType.Equals).ToArray();

        if (names.Length == 1)
            return SearchByAttribute(names[0], values.Length > 0 ? values[0] : "", types.Length > 0 ? types[0] : SearchType.Equals, query.Skip, query.Take);

        return MultiSearch(names, values, types, query.Skip, query.Take);
    }

    private ServiceResult<List<GameDto>> SearchByAttribute(string attributeName, string attributeValue, SearchType searchType, int skip, int take)
    {
        try
        {
            string tableName = FindAttributeTable(attributeName);
            if (tableName.Length == 0)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{attributeName}' не найден");

            string sql = BuildQuery(attributeName, attributeValue, searchType, tableName) + $" OFFSET {skip} LIMIT {take}";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            var games = new List<GameDto>();
            while (reader.Read())
            {
                var dto = new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1)
                };
                dto.properties[attributeName] = reader.IsDBNull(2) ? "" : reader.GetString(2);
                games.Add(dto);
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private ServiceResult<List<GameDto>> MultiSearch(string[] names, string[] values, SearchType[] types, int skip, int take)
    {
        try
        {
            var queries = new List<string>();

            for (int i = 0; i < names.Length; i++)
            {
                string tableName = FindAttributeTable(names[i]);
                if (tableName.Length == 0) continue;

                string val = values.Length > i ? values[i] : "";
                SearchType type = types.Length > i ? types[i] : SearchType.Equals;

                if (tableName == "Game")
                    queries.Add(BuildGameFieldSelect(names[i], val, type));
                else
                {
                    string alias = $"attr{i}";
                    string where = BuildWhereClause(tableName, alias, type, val);
                    queries.Add($"SELECT DISTINCT g.game_id, g.title FROM Game g INNER JOIN {tableName} {alias} ON g.game_id = {alias}.game_id AND {alias}.attribute_name = '{names[i].Replace("'", "''")}' WHERE {where}");
                }
            }

            if (queries.Count == 0)
                return ServiceResult<List<GameDto>>.Fail("Ни один атрибут не найден");

            string sql = string.Join(" INTERSECT ", queries) + $" ORDER BY title OFFSET {skip} LIMIT {take}";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            var games = new List<GameDto>();
            while (reader.Read())
            {
                var dto = new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1)
                };

                foreach (var name in names)
                    dto.properties[name] = GetAttributeValue(dto.game_id.ToString(), name);

                games.Add(dto);
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private string FindAttributeTable(string attributeName)
    {
        var gameFields = new[] { "title", "release_date", "developer", "publisher", "base_price" };
        if (gameFields.Contains(attributeName.ToLower()))
            return "Game";

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
        return "";
    }

    private string BuildQuery(string attributeName, string attributeValue, SearchType searchType, string tableName)
    {
        if (tableName == "Game")
            return BuildGameFieldQuery(attributeName, attributeValue, searchType);

        string selectClause = "SELECT DISTINCT g.game_id, g.title, attr.attribute_value::TEXT FROM Game g";
        string joinClause = $"INNER JOIN {tableName} attr ON g.game_id = attr.game_id AND attr.attribute_name = '{attributeName.Replace("'", "''")}'";
        string whereClause = BuildWhereClause(tableName, "attr", searchType, attributeValue);

        return $"{selectClause} {joinClause} WHERE {whereClause} ORDER BY g.title";
    }

    private string BuildGameFieldQuery(string attributeName, string attributeValue, SearchType searchType)
    {
        string selectClause = $"SELECT g.game_id, g.title, CAST(g.{attributeName} AS TEXT) FROM Game g";
        string val = attributeValue.Replace("'", "''");

        string whereClause = searchType switch
        {
            SearchType.Equals => $"CAST(g.{attributeName} AS TEXT) = '{val}'",
            SearchType.NotEquals => $"CAST(g.{attributeName} AS TEXT) != '{val}'",
            SearchType.GreaterThan => attributeName == "release_date"
                ? $"g.{attributeName} > '{val}'::DATE"
                : $"g.{attributeName} > {val}::DECIMAL",
            SearchType.LessThan => attributeName == "release_date"
                ? $"g.{attributeName} < '{val}'::DATE"
                : $"g.{attributeName} < {val}::DECIMAL",
            SearchType.GreaterOrEqual => attributeName == "release_date"
                ? $"g.{attributeName} >= '{val}'::DATE"
                : $"g.{attributeName} >= {val}::DECIMAL",
            SearchType.LessOrEqual => attributeName == "release_date"
                ? $"g.{attributeName} <= '{val}'::DATE"
                : $"g.{attributeName} <= {val}::DECIMAL",
            SearchType.Contains => $"CAST(g.{attributeName} AS TEXT) LIKE '%{val}%'",
            SearchType.NotContains => $"CAST(g.{attributeName} AS TEXT) NOT LIKE '%{val}%'",
            _ => "1=1"
        };

        return $"{selectClause} WHERE {whereClause} ORDER BY g.title";
    }

    private string BuildGameFieldSelect(string attributeName, string attributeValue, SearchType searchType)
    {
        string val = attributeValue.Replace("'", "''");

        string whereClause = searchType switch
        {
            SearchType.Equals => $"CAST(g.{attributeName} AS TEXT) = '{val}'",
            SearchType.NotEquals => $"CAST(g.{attributeName} AS TEXT) != '{val}'",
            SearchType.GreaterThan => attributeName == "release_date"
                ? $"g.{attributeName} > '{val}'::DATE"
                : $"g.{attributeName} > {val}::DECIMAL",
            SearchType.LessThan => attributeName == "release_date"
                ? $"g.{attributeName} < '{val}'::DATE"
                : $"g.{attributeName} < {val}::DECIMAL",
            SearchType.GreaterOrEqual => attributeName == "release_date"
                ? $"g.{attributeName} >= '{val}'::DATE"
                : $"g.{attributeName} >= {val}::DECIMAL",
            SearchType.LessOrEqual => attributeName == "release_date"
                ? $"g.{attributeName} <= '{val}'::DATE"
                : $"g.{attributeName} <= {val}::DECIMAL",
            SearchType.Contains => $"CAST(g.{attributeName} AS TEXT) LIKE '%{val}%'",
            SearchType.NotContains => $"CAST(g.{attributeName} AS TEXT) NOT LIKE '%{val}%'",
            _ => "1=1"
        };

        return $"SELECT DISTINCT g.game_id, g.title FROM Game g WHERE {whereClause}";
    }

    private string BuildWhereClause(string tableName, string alias, SearchType searchType, string value)
    {
        string val = value.Replace("'", "''");

        return searchType switch
        {
            SearchType.Equals => tableName switch
            {
                "attributeboolean" => $"{alias}.attribute_value = {val.ToUpper()}",
                _ => $"CAST({alias}.attribute_value AS TEXT) = '{val}'"
            },
            SearchType.NotEquals => $"CAST({alias}.attribute_value AS TEXT) != '{val}'",
            SearchType.GreaterThan => tableName switch
            {
                "attributedate" => $"{alias}.attribute_value > '{val}'::DATE",
                _ => $"{alias}.attribute_value > {val}::DECIMAL"
            },
            SearchType.LessThan => tableName switch
            {
                "attributedate" => $"{alias}.attribute_value < '{val}'::DATE",
                _ => $"{alias}.attribute_value < {val}::DECIMAL"
            },
            SearchType.GreaterOrEqual => tableName switch
            {
                "attributedate" => $"{alias}.attribute_value >= '{val}'::DATE",
                _ => $"{alias}.attribute_value >= {val}::DECIMAL"
            },
            SearchType.LessOrEqual => tableName switch
            {
                "attributedate" => $"{alias}.attribute_value <= '{val}'::DATE",
                _ => $"{alias}.attribute_value <= {val}::DECIMAL"
            },
            SearchType.Between => tableName switch
            {
                "attributedate" => BuildBetweenDates(alias, value),
                _ => BuildBetweenNumbers(alias, value)
            },
            SearchType.Contains => $"CAST({alias}.attribute_value AS TEXT) LIKE '%{val}%'",
            SearchType.NotContains => $"CAST({alias}.attribute_value AS TEXT) NOT LIKE '%{val}%'",
            _ => "1=1"
        };
    }

    private string BuildBetweenDates(string alias, string value)
    {
        var parts = value.Replace("{", "").Replace("}", "").Split(',');
        if (parts.Length == 2)
            return $"{alias}.attribute_value BETWEEN '{parts[0].Trim()}'::DATE AND '{parts[1].Trim()}'::DATE";
        return "1=1";
    }

    private string BuildBetweenNumbers(string alias, string value)
    {
        var parts = value.Replace("{", "").Replace("}", "").Split(',');
        if (parts.Length == 2)
            return $"{alias}.attribute_value BETWEEN {parts[0].Trim()}::DECIMAL AND {parts[1].Trim()}::DECIMAL";
        return "1=1";
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
                var dto = new GameDto
                {
                    game_id = reader.GetInt32(0),
                    title = reader.GetString(1)
                };
                dto.properties["release_date"] = reader.IsDBNull(2) ? "" : reader.GetDateTime(2).ToString("yyyy-MM-dd");
                dto.properties["developer"] = reader.IsDBNull(3) ? "" : reader.GetString(3);
                dto.properties["publisher"] = reader.IsDBNull(4) ? "" : reader.GetString(4);
                dto.properties["base_price"] = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);
                games.Add(dto);
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    public string GetAttributeValue(string gameId, string attributeName)
    {
        string tableName = FindAttributeTable(attributeName);
        if (tableName.Length == 0) return "";

        if (tableName == "Game")
        {
            string sql = $"SELECT CAST({attributeName} AS TEXT) FROM Game WHERE game_id = @gameId LIMIT 1";
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@gameId", int.Parse(gameId));
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        string attrSql = $"SELECT attribute_value::TEXT FROM {tableName} WHERE game_id = @gameId AND attribute_name = @attrName LIMIT 1";
        using var conn2 = new NpgsqlConnection(_connectionString);
        conn2.Open();
        using var cmd2 = new NpgsqlCommand(attrSql, conn2);
        cmd2.Parameters.AddWithValue("@gameId", int.Parse(gameId));
        cmd2.Parameters.AddWithValue("@attrName", attributeName);
        return cmd2.ExecuteScalar()?.ToString() ?? "";
    }
}