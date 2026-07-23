using Microsoft.EntityFrameworkCore;
using SQLvsORM.Enums;
using SQLvsORM.Model.DbEntities;
using SQLvsORM.Model.DTOs;
using SQLvsORM.Model;
using SQLvsORM.Models;

namespace SQLvsORM.Services;

public class SearchServiceEF
{
    private readonly GameDbContext _context;

    public SearchServiceEF(GameDbContext context)
    {
        _context = context;
    }

    public ServiceResult<List<GameDto>> Search(SearchQuery query)
    {
        var names = query.AttributeNames.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToArray();

        if (names.Length == 0)
            return GetAllGames(query.Skip, query.Take);

        var values = query.AttributeValues.Split(',').Select(v => v.Trim()).ToArray();
        var types = query.SearchTypes.Split(',').Select(t => int.TryParse(t.Trim(), out var tp) ? (SearchType)tp : SearchType.Equals).ToArray();

        if (names.Length == 1)
        {
            var tableType = FindAttributeTable(names[0]);
            if (tableType == AttributeTableType.None)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{names[0]}' не найден");

            string val = values.Length > 0 ? values[0] : "";
            SearchType type = types.Length > 0 ? types[0] : SearchType.Equals;

            var result = SearchSingleJoin(names[0], val, type, tableType, query.Skip, query.Take);
            return ServiceResult<List<GameDto>>.Success(result);
        }

        return MultiSearch(names, values, types, query.Skip, query.Take);
    }

    private ServiceResult<List<GameDto>> MultiSearch(string[] names, string[] values, SearchType[] types, int skip, int take)
    {
        try
        {
            List<int> resultIds = null;

            for (int i = 0; i < names.Length; i++)
            {
                var tableType = FindAttributeTable(names[i]);
                if (tableType == AttributeTableType.None) continue;

                string val = values.Length > i ? values[i] : "";
                SearchType type = types.Length > i ? types[i] : SearchType.Equals;

                var ids = GetGameIdsBySearch(names[i], val, type, tableType);

                if (resultIds == null)
                    resultIds = ids;
                else
                    resultIds = resultIds.Intersect(ids).ToList();
            }

            if (resultIds == null || resultIds.Count == 0)
                return ServiceResult<List<GameDto>>.Fail("Ничего не найдено");

            var games = _context.Games
                .Where(g => resultIds.Contains(g.game_id))
                .OrderBy(g => g.title)
                .Skip(skip)
                .Take(take)
                .Select(g => new GameDto { game_id = g.game_id, title = g.title })
                .ToList();

            foreach (var game in games)
            {
                foreach (var name in names)
                    game.properties[name] = GetAttributeValue(game.game_id.ToString(), name);
            }

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private enum AttributeTableType { None, Text, Number, Boolean, Date, Game }

    private AttributeTableType FindAttributeTable(string attributeName)
    {
        var gameFields = new[] { "title", "release_date", "developer", "publisher", "base_price" };
        if (gameFields.Contains(attributeName.ToLower()))
            return AttributeTableType.Game;

        if (_context.AttributeTexts.Any(a => a.attribute_name == attributeName)) return AttributeTableType.Text;
        if (_context.AttributeNumbers.Any(a => a.attribute_name == attributeName)) return AttributeTableType.Number;
        if (_context.AttributeBooleans.Any(a => a.attribute_name == attributeName)) return AttributeTableType.Boolean;
        if (_context.AttributeDates.Any(a => a.attribute_name == attributeName)) return AttributeTableType.Date;
        return AttributeTableType.None;
    }

    private List<int> GetGameIdsBySearch(string attributeName, string attributeValue, SearchType searchType, AttributeTableType tableType)
    {
        if (tableType == AttributeTableType.Game)
            return SearchGameFieldIds(attributeName, attributeValue, searchType);

        return tableType switch
        {
            AttributeTableType.Text => SearchTextIds(attributeName, attributeValue, searchType),
            AttributeTableType.Number => SearchNumberIds(attributeName, attributeValue, searchType),
            AttributeTableType.Boolean => SearchBooleanIds(attributeName, attributeValue, searchType),
            AttributeTableType.Date => SearchDateIds(attributeName, attributeValue, searchType),
            _ => new List<int>()
        };
    }

    private List<int> SearchTextIds(string attributeName, string value, SearchType searchType)
    {
        var q = _context.AttributeTexts.Where(a => a.attribute_name == attributeName);

        switch (searchType)
        {
            case SearchType.Equals: return q.Where(a => a.attribute_value == value).Select(a => a.game_id).Distinct().ToList();
            case SearchType.NotEquals: return q.Where(a => a.attribute_value != value).Select(a => a.game_id).Distinct().ToList();
            case SearchType.Contains: return q.Where(a => a.attribute_value.Contains(value)).Select(a => a.game_id).Distinct().ToList();
            case SearchType.NotContains: return q.Where(a => !a.attribute_value.Contains(value)).Select(a => a.game_id).Distinct().ToList();
            default: return q.Select(a => a.game_id).Distinct().ToList();
        }
    }

    private List<int> SearchNumberIds(string attributeName, string value, SearchType searchType)
    {
        var q = _context.AttributeNumbers.Where(a => a.attribute_name == attributeName);
        decimal val = decimal.TryParse(value, out var v) ? v : 0;

        switch (searchType)
        {
            case SearchType.Equals: return q.Where(a => a.attribute_value == val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.NotEquals: return q.Where(a => a.attribute_value != val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.GreaterThan: return q.Where(a => a.attribute_value > val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.LessThan: return q.Where(a => a.attribute_value < val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.GreaterOrEqual: return q.Where(a => a.attribute_value >= val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.LessOrEqual: return q.Where(a => a.attribute_value <= val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.Between:
                var parts = value.Replace("{", "").Replace("}", "").Split(',');
                if (parts.Length == 2 && decimal.TryParse(parts[0].Trim(), out var v1) && decimal.TryParse(parts[1].Trim(), out var v2))
                    return q.Where(a => a.attribute_value >= v1 && a.attribute_value <= v2).Select(a => a.game_id).Distinct().ToList();
                return q.Select(a => a.game_id).Distinct().ToList();
            default: return q.Select(a => a.game_id).Distinct().ToList();
        }
    }

    private List<int> SearchBooleanIds(string attributeName, string value, SearchType searchType)
    {
        var q = _context.AttributeBooleans.Where(a => a.attribute_name == attributeName);
        bool val = bool.TryParse(value, out var b) && b;

        switch (searchType)
        {
            case SearchType.Equals: return q.Where(a => a.attribute_value == val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.NotEquals: return q.Where(a => a.attribute_value != val).Select(a => a.game_id).Distinct().ToList();
            default: return q.Select(a => a.game_id).Distinct().ToList();
        }
    }

    private List<int> SearchDateIds(string attributeName, string value, SearchType searchType)
    {
        var q = _context.AttributeDates.Where(a => a.attribute_name == attributeName);
        DateTime val = DateTime.TryParse(value, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.MinValue;

        switch (searchType)
        {
            case SearchType.Equals: return q.Where(a => a.attribute_value == val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.NotEquals: return q.Where(a => a.attribute_value != val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.GreaterThan: return q.Where(a => a.attribute_value > val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.LessThan: return q.Where(a => a.attribute_value < val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.GreaterOrEqual: return q.Where(a => a.attribute_value >= val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.LessOrEqual: return q.Where(a => a.attribute_value <= val).Select(a => a.game_id).Distinct().ToList();
            case SearchType.Between:
                var parts = value.Replace("{", "").Replace("}", "").Split(',');
                if (parts.Length == 2 && DateTime.TryParse(parts[0].Trim(), out var d1) && DateTime.TryParse(parts[1].Trim(), out var d2))
                {
                    d1 = DateTime.SpecifyKind(d1, DateTimeKind.Utc);
                    d2 = DateTime.SpecifyKind(d2, DateTimeKind.Utc);
                    return q.Where(a => a.attribute_value >= d1 && a.attribute_value <= d2).Select(a => a.game_id).Distinct().ToList();
                }
                return q.Select(a => a.game_id).Distinct().ToList();
            default: return q.Select(a => a.game_id).Distinct().ToList();
        }
    }

    private List<int> SearchGameFieldIds(string field, string value, SearchType searchType)
    {
        IQueryable<Game> q = _context.Games;

        switch (searchType)
        {
            case SearchType.Equals:
                q = q.Where(g => EF.Property<string>(g, field) == value);
                break;
            case SearchType.NotEquals:
                q = q.Where(g => EF.Property<string>(g, field) != value);
                break;
            case SearchType.Contains:
                q = q.Where(g => EF.Property<string>(g, field).Contains(value));
                break;
            case SearchType.NotContains:
                q = q.Where(g => !EF.Property<string>(g, field).Contains(value));
                break;
            case SearchType.GreaterThan:
                if (field == "release_date")
                    q = q.Where(g => g.release_date > DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) > decimal.Parse(value));
                break;
            case SearchType.LessThan:
                if (field == "release_date")
                    q = q.Where(g => g.release_date < DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) < decimal.Parse(value));
                break;
            case SearchType.GreaterOrEqual:
                if (field == "release_date")
                    q = q.Where(g => g.release_date >= DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) >= decimal.Parse(value));
                break;
            case SearchType.LessOrEqual:
                if (field == "release_date")
                    q = q.Where(g => g.release_date <= DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) <= decimal.Parse(value));
                break;
        }

        return q.Select(g => g.game_id).Distinct().ToList();
    }

    private List<GameDto> SearchSingleJoin(string attributeName, string attributeValue, SearchType searchType, AttributeTableType tableType, int skip, int take)
    {
        if (tableType == AttributeTableType.Game)
            return SearchGameFieldJoin(attributeName, attributeValue, searchType, skip, take);

        return tableType switch
        {
            AttributeTableType.Text => SearchTextJoin(attributeName, attributeValue, searchType, skip, take),
            AttributeTableType.Number => SearchNumberJoin(attributeName, attributeValue, searchType, skip, take),
            AttributeTableType.Boolean => SearchBooleanJoin(attributeName, attributeValue, searchType, skip, take),
            AttributeTableType.Date => SearchDateJoin(attributeName, attributeValue, searchType, skip, take),
            _ => new List<GameDto>()
        };
    }

    private List<GameDto> SearchTextJoin(string attributeName, string value, SearchType searchType, int skip, int take)
    {
        var baseQ = from g in _context.Games
                    join a in _context.AttributeTexts on g.game_id equals a.game_id
                    where a.attribute_name == attributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (searchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == value).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != value).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.Contains:
                result = baseQ.Where(x => x.a.attribute_value.Contains(value)).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.NotContains:
                result = baseQ.Where(x => !x.a.attribute_value.Contains(value)).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
        }

        var games = result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();

        foreach (var game in games)
            game.properties[attributeName] = GetAttributeValue(game.game_id.ToString(), attributeName);

        return games;
    }

    private List<GameDto> SearchNumberJoin(string attributeName, string value, SearchType searchType, int skip, int take)
    {
        decimal val = decimal.TryParse(value, out var v) ? v : 0;

        var baseQ = from g in _context.Games
                    join a in _context.AttributeNumbers on g.game_id equals a.game_id
                    where a.attribute_name == attributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (searchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.GreaterThan:
                result = baseQ.Where(x => x.a.attribute_value > val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.LessThan:
                result = baseQ.Where(x => x.a.attribute_value < val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.GreaterOrEqual:
                result = baseQ.Where(x => x.a.attribute_value >= val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.LessOrEqual:
                result = baseQ.Where(x => x.a.attribute_value <= val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.Between:
                var parts = value.Replace("{", "").Replace("}", "").Split(',');
                if (parts.Length == 2 && decimal.TryParse(parts[0].Trim(), out var v1) && decimal.TryParse(parts[1].Trim(), out var v2))
                    result = baseQ.Where(x => x.a.attribute_value >= v1 && x.a.attribute_value <= v2).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                else
                    result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
        }

        var games = result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();

        foreach (var game in games)
            game.properties[attributeName] = GetAttributeValue(game.game_id.ToString(), attributeName);

        return games;
    }

    private List<GameDto> SearchBooleanJoin(string attributeName, string value, SearchType searchType, int skip, int take)
    {
        bool val = bool.TryParse(value, out var b) && b;

        var baseQ = from g in _context.Games
                    join a in _context.AttributeBooleans on g.game_id equals a.game_id
                    where a.attribute_name == attributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (searchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
        }

        var games = result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();

        foreach (var game in games)
            game.properties[attributeName] = GetAttributeValue(game.game_id.ToString(), attributeName);

        return games;
    }

    private List<GameDto> SearchDateJoin(string attributeName, string value, SearchType searchType, int skip, int take)
    {
        DateTime val = DateTime.TryParse(value, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.MinValue;

        var baseQ = from g in _context.Games
                    join a in _context.AttributeDates on g.game_id equals a.game_id
                    where a.attribute_name == attributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (searchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.GreaterThan:
                result = baseQ.Where(x => x.a.attribute_value > val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.LessThan:
                result = baseQ.Where(x => x.a.attribute_value < val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.GreaterOrEqual:
                result = baseQ.Where(x => x.a.attribute_value >= val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.LessOrEqual:
                result = baseQ.Where(x => x.a.attribute_value <= val).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            case SearchType.Between:
                var parts = value.Replace("{", "").Replace("}", "").Split(',');
                if (parts.Length == 2 && DateTime.TryParse(parts[0].Trim(), out var d1) && DateTime.TryParse(parts[1].Trim(), out var d2))
                {
                    d1 = DateTime.SpecifyKind(d1, DateTimeKind.Utc);
                    d2 = DateTime.SpecifyKind(d2, DateTimeKind.Utc);
                    result = baseQ.Where(x => x.a.attribute_value >= d1 && x.a.attribute_value <= d2).Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                }
                else
                    result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title });
                break;
        }

        var games = result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();

        foreach (var game in games)
            game.properties[attributeName] = GetAttributeValue(game.game_id.ToString(), attributeName);

        return games;
    }

    private List<GameDto> SearchGameFieldJoin(string field, string value, SearchType searchType, int skip, int take)
    {
        IQueryable<Game> q = _context.Games;

        switch (searchType)
        {
            case SearchType.Equals:
                q = q.Where(g => EF.Property<string>(g, field) == value);
                break;
            case SearchType.NotEquals:
                q = q.Where(g => EF.Property<string>(g, field) != value);
                break;
            case SearchType.Contains:
                q = q.Where(g => EF.Property<string>(g, field).Contains(value));
                break;
            case SearchType.NotContains:
                q = q.Where(g => !EF.Property<string>(g, field).Contains(value));
                break;
            case SearchType.GreaterThan:
                if (field == "release_date")
                    q = q.Where(g => g.release_date > DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) > decimal.Parse(value));
                break;
            case SearchType.LessThan:
                if (field == "release_date")
                    q = q.Where(g => g.release_date < DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) < decimal.Parse(value));
                break;
            case SearchType.GreaterOrEqual:
                if (field == "release_date")
                    q = q.Where(g => g.release_date >= DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) >= decimal.Parse(value));
                break;
            case SearchType.LessOrEqual:
                if (field == "release_date")
                    q = q.Where(g => g.release_date <= DateTime.Parse(value));
                else
                    q = q.Where(g => EF.Property<decimal>(g, field) <= decimal.Parse(value));
                break;
        }

        var games = q
            .OrderBy(g => g.title)
            .Skip(skip)
            .Take(take)
            .Select(g => new GameDto { game_id = g.game_id, title = g.title })
            .ToList();

        foreach (var game in games)
            game.properties[field] = GetAttributeValue(game.game_id.ToString(), field);

        return games;
    }

    public ServiceResult<List<GameDto>> GetAllGames(int skip = 0, int take = 100)
    {
        try
        {
            var games = _context.Games
                .OrderBy(g => g.title)
                .Skip(skip)
                .Take(take)
                .Select(g => new GameDto
                {
                    game_id = g.game_id,
                    title = g.title
                })
                .ToList();

            foreach (var game in games)
            {
                var entity = _context.Games.FirstOrDefault(g => g.game_id == game.game_id);
                if (entity != null)
                {
                    game.properties["release_date"] = entity.release_date?.ToString("yyyy-MM-dd") ?? "";
                    game.properties["developer"] = entity.developer ?? "";
                    game.properties["publisher"] = entity.publisher ?? "";
                    game.properties["base_price"] = entity.base_price ?? 0;
                }
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
        int id = int.Parse(gameId);
        var tableType = FindAttributeTable(attributeName);

        return tableType switch
        {
            AttributeTableType.Text => _context.AttributeTexts.Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value).FirstOrDefault() ?? "",
            AttributeTableType.Number => _context.AttributeNumbers.Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString()).FirstOrDefault() ?? "",
            AttributeTableType.Boolean => _context.AttributeBooleans.Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString()).FirstOrDefault() ?? "",
            AttributeTableType.Date => _context.AttributeDates.Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString("yyyy-MM-dd")).FirstOrDefault() ?? "",
            AttributeTableType.Game => GetGameFieldValue(id, attributeName),
            _ => ""
        };
    }

    private string GetGameFieldValue(int gameId, string field)
    {
        var game = _context.Games.FirstOrDefault(g => g.game_id == gameId);
        if (game == null) return "";

        return field.ToLower() switch
        {
            "title" => game.title ?? "",
            "release_date" => game.release_date?.ToString("yyyy-MM-dd") ?? "",
            "developer" => game.developer ?? "",
            "publisher" => game.publisher ?? "",
            "base_price" => game.base_price?.ToString() ?? "",
            _ => ""
        };
    }
}