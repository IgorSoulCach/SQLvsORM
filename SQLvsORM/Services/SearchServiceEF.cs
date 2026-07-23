using Microsoft.EntityFrameworkCore;
using SQLvsORM.Enums;
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
            var tableType = FindAttributeTable(attributeName);
            if (tableType == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{attributeName}' не найден");

            var gameIds = GetGameIdsWithAttribute(tableType.Value, attributeName);

            var games = _context.Games
                .AsNoTracking()
                .Where(g => gameIds.Contains(g.game_id))
                .OrderBy(g => g.title)
                .Skip(skip)
                .Take(take)
                .Select(g => new GameDto
                {
                    game_id = g.game_id,
                    title = g.title
                })
                .ToList();

            var ids = games.Select(g => g.game_id).ToList();
            var attrValues = GetAttributeValuesBatch(ids, attributeName, tableType.Value);

            foreach (var game in games)
                game.attribute_value = attrValues.GetValueOrDefault(game.game_id, string.Empty);

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
            var tableType = FindAttributeTable(query.AttributeName);
            if (tableType == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{query.AttributeName}' не найден");

            var games = SearchAndJoin(tableType.Value, query, skip, take);

            return ServiceResult<List<GameDto>>.Success(games);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<GameDto>>.Fail(ex.Message);
        }
    }

    private List<GameDto> SearchAndJoin(AttributeTableType tableType, SearchQuery query, int skip, int take)
    {
        return tableType switch
        {
            AttributeTableType.Text => SearchTextJoin(query, skip, take),
            AttributeTableType.Number => SearchNumberJoin(query, skip, take),
            AttributeTableType.Boolean => SearchBooleanJoin(query, skip, take),
            AttributeTableType.Date => SearchDateJoin(query, skip, take),
            _ => new List<GameDto>()
        };
    }

    private List<GameDto> SearchTextJoin(SearchQuery query, int skip, int take)
    {
        var value = query.AttributeValue ?? "";
        var valuesList = value.Split(',').Select(v => v.Trim()).ToList();

        var baseQ = from g in _context.Games.AsNoTracking()
                    join a in _context.AttributeTexts.AsNoTracking() on g.game_id equals a.game_id
                    where a.attribute_name == query.AttributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (query.SearchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == value)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != value)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value });
                break;
            case SearchType.Contains:
                result = baseQ.Where(x => x.a.attribute_value.Contains(value) || valuesList.Contains(x.a.attribute_value))
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value });
                break;
            case SearchType.NotContains:
                result = baseQ.Where(x => !x.a.attribute_value.Contains(value) && !valuesList.Contains(x.a.attribute_value))
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value });
                break;
        }

        return result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();
    }

    private List<GameDto> SearchNumberJoin(SearchQuery query, int skip, int take)
    {
        decimal val = decimal.TryParse(query.AttributeValue, out var v) ? v : 0;
        decimal val2 = decimal.TryParse(query.AttributeValue2, out var v2) ? v2 : 0;
        var valuesList = (query.AttributeValue ?? "").Split(',').Select(s => s.Trim()).ToList();

        var baseQ = from g in _context.Games.AsNoTracking()
                    join a in _context.AttributeNumbers.AsNoTracking() on g.game_id equals a.game_id
                    where a.attribute_name == query.AttributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (query.SearchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.GreaterThan:
                result = baseQ.Where(x => x.a.attribute_value > val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.LessThan:
                result = baseQ.Where(x => x.a.attribute_value < val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.GreaterOrEqual:
                result = baseQ.Where(x => x.a.attribute_value >= val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.LessOrEqual:
                result = baseQ.Where(x => x.a.attribute_value <= val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.Between:
                result = baseQ.Where(x => x.a.attribute_value >= val && x.a.attribute_value <= val2)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.Contains:
                result = baseQ.Where(x => valuesList.Contains(x.a.attribute_value.ToString()) || x.a.attribute_value.ToString().Contains(query.AttributeValue ?? ""))
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.NotContains:
                result = baseQ.Where(x => !valuesList.Contains(x.a.attribute_value.ToString()) && !x.a.attribute_value.ToString().Contains(query.AttributeValue ?? ""))
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
        }

        return result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();
    }

    private List<GameDto> SearchBooleanJoin(SearchQuery query, int skip, int take)
    {
        bool val = bool.TryParse(query.AttributeValue, out var b) && b;

        var baseQ = from g in _context.Games.AsNoTracking()
                    join a in _context.AttributeBooleans.AsNoTracking() on g.game_id equals a.game_id
                    where a.attribute_name == query.AttributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (query.SearchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString() });
                break;
        }

        return result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();
    }

    private List<GameDto> SearchDateJoin(SearchQuery query, int skip, int take)
    {
        DateTime val = DateTime.TryParse(query.AttributeValue, out var d) ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : DateTime.MinValue;
        DateTime val2 = DateTime.TryParse(query.AttributeValue2, out var d2) ? DateTime.SpecifyKind(d2, DateTimeKind.Utc) : DateTime.MinValue;

        var baseQ = from g in _context.Games.AsNoTracking()
                    join a in _context.AttributeDates.AsNoTracking() on g.game_id equals a.game_id
                    where a.attribute_name == query.AttributeName
                    select new { g, a };

        IQueryable<GameDto> result;

        switch (query.SearchType)
        {
            case SearchType.Equals:
                result = baseQ.Where(x => x.a.attribute_value == val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.NotEquals:
                result = baseQ.Where(x => x.a.attribute_value != val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.GreaterThan:
                result = baseQ.Where(x => x.a.attribute_value > val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.LessThan:
                result = baseQ.Where(x => x.a.attribute_value < val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.GreaterOrEqual:
                result = baseQ.Where(x => x.a.attribute_value >= val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.LessOrEqual:
                result = baseQ.Where(x => x.a.attribute_value <= val)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            case SearchType.Between:
                result = baseQ.Where(x => x.a.attribute_value >= val && x.a.attribute_value <= val2)
                    .Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
            default:
                result = baseQ.Select(x => new GameDto { game_id = x.g.game_id, title = x.g.title, attribute_value = x.a.attribute_value.ToString("yyyy-MM-dd") });
                break;
        }

        return result.OrderBy(x => x.title).Skip(skip).Take(take).ToList();
    }

    public ServiceResult<List<GameDto>> GetAllGames(int skip = 0, int take = 100)
    {
        try
        {
            var games = _context.Games
                .AsNoTracking()
                .OrderBy(g => g.title)
                .Skip(skip)
                .Take(take)
                .Select(g => new GameDto
                {
                    game_id = g.game_id,
                    title = g.title,
                    release_date = g.release_date.HasValue ? g.release_date.Value.ToString("yyyy-MM-dd") : string.Empty,
                    developer = g.developer ?? string.Empty,
                    publisher = g.publisher ?? string.Empty,
                    base_price = g.base_price ?? 0
                })
                .ToList();

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
            var game = _context.Games
                .AsNoTracking()
                .Where(g => g.game_id == id)
                .Select(g => new GameDto
                {
                    game_id = g.game_id,
                    title = g.title,
                    release_date = g.release_date.HasValue ? g.release_date.Value.ToString("yyyy-MM-dd") : string.Empty,
                    developer = g.developer ?? string.Empty,
                    publisher = g.publisher ?? string.Empty,
                    base_price = g.base_price ?? 0
                })
                .FirstOrDefault();

            if (game == null)
                return ServiceResult<GameDto>.Fail($"Игра с ID {id} не найдена");

            return ServiceResult<GameDto>.Success(game);
        }
        catch (Exception ex)
        {
            return ServiceResult<GameDto>.Fail(ex.Message);
        }
    }

    public string GetAttributeValue(string gameId, string attributeName)
    {
        int id = int.Parse(gameId);
        var tableType = FindAttributeTable(attributeName);
        if (tableType == null) return string.Empty;

        return tableType switch
        {
            AttributeTableType.Text => _context.AttributeTexts.AsNoTracking().Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value).FirstOrDefault() ?? string.Empty,
            AttributeTableType.Number => _context.AttributeNumbers.AsNoTracking().Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString()).FirstOrDefault() ?? string.Empty,
            AttributeTableType.Boolean => _context.AttributeBooleans.AsNoTracking().Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString()).FirstOrDefault() ?? string.Empty,
            AttributeTableType.Date => _context.AttributeDates.AsNoTracking().Where(a => a.game_id == id && a.attribute_name == attributeName).Select(a => a.attribute_value.ToString("yyyy-MM-dd")).FirstOrDefault() ?? string.Empty,
            _ => string.Empty
        };
    }

    public Dictionary<string, List<string>> GetAllAttributes()
    {
        return new Dictionary<string, List<string>>
        {
            ["text"] = _context.AttributeTexts.AsNoTracking().Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["number"] = _context.AttributeNumbers.AsNoTracking().Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["boolean"] = _context.AttributeBooleans.AsNoTracking().Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["date"] = _context.AttributeDates.AsNoTracking().Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList()
        };
    }

    private enum AttributeTableType { Text, Number, Boolean, Date }

    private AttributeTableType? FindAttributeTable(string attributeName)
    {
        if (_context.AttributeTexts.AsNoTracking().Any(a => a.attribute_name == attributeName)) return AttributeTableType.Text;
        if (_context.AttributeNumbers.AsNoTracking().Any(a => a.attribute_name == attributeName)) return AttributeTableType.Number;
        if (_context.AttributeBooleans.AsNoTracking().Any(a => a.attribute_name == attributeName)) return AttributeTableType.Boolean;
        if (_context.AttributeDates.AsNoTracking().Any(a => a.attribute_name == attributeName)) return AttributeTableType.Date;
        return null;
    }

    private List<int> GetGameIdsWithAttribute(AttributeTableType tableType, string attributeName)
    {
        return tableType switch
        {
            AttributeTableType.Text => _context.AttributeTexts.AsNoTracking().Where(a => a.attribute_name == attributeName).Select(a => a.game_id).Distinct().ToList(),
            AttributeTableType.Number => _context.AttributeNumbers.AsNoTracking().Where(a => a.attribute_name == attributeName).Select(a => a.game_id).Distinct().ToList(),
            AttributeTableType.Boolean => _context.AttributeBooleans.AsNoTracking().Where(a => a.attribute_name == attributeName).Select(a => a.game_id).Distinct().ToList(),
            AttributeTableType.Date => _context.AttributeDates.AsNoTracking().Where(a => a.attribute_name == attributeName).Select(a => a.game_id).Distinct().ToList(),
            _ => new List<int>()
        };
    }

    private Dictionary<int, string> GetAttributeValuesBatch(List<int> gameIds, string attributeName, AttributeTableType tableType)
    {
        if (gameIds.Count == 0) return new Dictionary<int, string>();

        return tableType switch
        {
            AttributeTableType.Text => _context.AttributeTexts.AsNoTracking().Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName).ToDictionary(a => a.game_id, a => a.attribute_value),
            AttributeTableType.Number => _context.AttributeNumbers.AsNoTracking().Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName).ToDictionary(a => a.game_id, a => a.attribute_value.ToString()),
            AttributeTableType.Boolean => _context.AttributeBooleans.AsNoTracking().Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName).ToDictionary(a => a.game_id, a => a.attribute_value.ToString()),
            AttributeTableType.Date => _context.AttributeDates.AsNoTracking().Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName).ToDictionary(a => a.game_id, a => a.attribute_value.ToString("yyyy-MM-dd")),
            _ => new Dictionary<int, string>()
        };
    }
}