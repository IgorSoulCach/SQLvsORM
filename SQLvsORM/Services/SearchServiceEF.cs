using Microsoft.EntityFrameworkCore;
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
            var tableType = FindAttributeTable(attributeName);
            if (tableType == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{attributeName}' не найден");

            var gameIds = GetGameIdsWithAttribute(tableType.Value, attributeName);

            var games = _context.Games
                .AsNoTracking()
                .Where(g => gameIds.Contains(g.game_id))
                .OrderBy(g => g.title)
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

    private ServiceResult<List<GameDto>> SearchByAttribute(SearchQuery query)
    {
        try
        {
            var tableType = FindAttributeTable(query.AttributeName);
            if (tableType == null)
                return ServiceResult<List<GameDto>>.Fail($"Атрибут '{query.AttributeName}' не найден");

            var gameIds = GetGameIdsBySearch(tableType.Value, query);

            var games = _context.Games
                .AsNoTracking()
                .Where(g => gameIds.Contains(g.game_id))
                .OrderBy(g => g.title)
                .Select(g => new GameDto
                {
                    game_id = g.game_id,
                    title = g.title
                })
                .ToList();

            var ids = games.Select(g => g.game_id).ToList();
            var attrValues = GetAttributeValuesBatch(ids, query.AttributeName, tableType.Value);

            foreach (var game in games)
                game.attribute_value = attrValues.GetValueOrDefault(game.game_id, string.Empty);

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
            var games = _context.Games
                .AsNoTracking()
                .OrderBy(g => g.title)
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
            AttributeTableType.Text => _context.AttributeTexts
                .AsNoTracking()
                .Where(a => a.game_id == id && a.attribute_name == attributeName)
                .Select(a => a.attribute_value)
                .FirstOrDefault() ?? string.Empty,
            AttributeTableType.Number => _context.AttributeNumbers
                .AsNoTracking()
                .Where(a => a.game_id == id && a.attribute_name == attributeName)
                .Select(a => a.attribute_value.ToString())
                .FirstOrDefault() ?? string.Empty,
            AttributeTableType.Boolean => _context.AttributeBooleans
                .AsNoTracking()
                .Where(a => a.game_id == id && a.attribute_name == attributeName)
                .Select(a => a.attribute_value.ToString())
                .FirstOrDefault() ?? string.Empty,
            AttributeTableType.Date => _context.AttributeDates
                .AsNoTracking()
                .Where(a => a.game_id == id && a.attribute_name == attributeName)
                .Select(a => a.attribute_value.ToString("yyyy-MM-dd"))
                .FirstOrDefault() ?? string.Empty,
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
        if (_context.AttributeTexts.AsNoTracking().Any(a => a.attribute_name == attributeName))
            return AttributeTableType.Text;
        if (_context.AttributeNumbers.AsNoTracking().Any(a => a.attribute_name == attributeName))
            return AttributeTableType.Number;
        if (_context.AttributeBooleans.AsNoTracking().Any(a => a.attribute_name == attributeName))
            return AttributeTableType.Boolean;
        if (_context.AttributeDates.AsNoTracking().Any(a => a.attribute_name == attributeName))
            return AttributeTableType.Date;
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

    private List<int> GetGameIdsBySearch(AttributeTableType tableType, SearchQuery query)
    {
        return tableType switch
        {
            AttributeTableType.Text => SearchText(query),
            AttributeTableType.Number => SearchNumber(query),
            AttributeTableType.Boolean => SearchBoolean(query),
            AttributeTableType.Date => SearchDate(query),
            _ => new List<int>()
        };
    }

    private List<int> SearchText(SearchQuery query)
    {
        var value = query.AttributeValue ?? "";

        if (query.SearchType == SearchType.In || query.SearchType == SearchType.NotIn)
        {
            var values = value.Split(',').Select(v => v.Trim()).ToList();

            if (query.SearchType == SearchType.In)
                return _context.AttributeTexts.AsNoTracking().Where(a => a.attribute_name == query.AttributeName && values.Contains(a.attribute_value)).Select(a => a.game_id).Distinct().ToList();
            else
                return _context.AttributeTexts.AsNoTracking().Where(a => a.attribute_name == query.AttributeName && !values.Contains(a.attribute_value)).Select(a => a.game_id).Distinct().ToList();
        }

        var q = _context.AttributeTexts.AsNoTracking().Where(a => a.attribute_name == query.AttributeName);

        q = query.SearchType switch
        {
            SearchType.Equals => q.Where(a => a.attribute_value == value),
            SearchType.NotEquals => q.Where(a => a.attribute_value != value),
            SearchType.Contains => q.Where(a => a.attribute_value.Contains(value)),
            _ => q
        };

        return q.Select(a => a.game_id).Distinct().ToList();
    }

    private List<int> SearchNumber(SearchQuery query)
    {
        var q = _context.AttributeNumbers.AsNoTracking().Where(a => a.attribute_name == query.AttributeName);
        decimal val = decimal.TryParse(query.AttributeValue, out var v) ? v : 0;
        decimal val2 = decimal.TryParse(query.AttributeValue2, out var v2) ? v2 : 0;

        q = query.SearchType switch
        {
            SearchType.Equals => q.Where(a => a.attribute_value == val),
            SearchType.NotEquals => q.Where(a => a.attribute_value != val),
            SearchType.GreaterThan => q.Where(a => a.attribute_value > val),
            SearchType.LessThan => q.Where(a => a.attribute_value < val),
            SearchType.GreaterOrEqual => q.Where(a => a.attribute_value >= val),
            SearchType.LessOrEqual => q.Where(a => a.attribute_value <= val),
            SearchType.Between => q.Where(a => a.attribute_value >= val && a.attribute_value <= val2),
            _ => q
        };

        return q.Select(a => a.game_id).Distinct().ToList();
    }

    private List<int> SearchBoolean(SearchQuery query)
    {
        var q = _context.AttributeBooleans.AsNoTracking().Where(a => a.attribute_name == query.AttributeName);
        bool val = bool.TryParse(query.AttributeValue, out var b) && b;

        q = query.SearchType switch
        {
            SearchType.Equals => q.Where(a => a.attribute_value == val),
            SearchType.NotEquals => q.Where(a => a.attribute_value != val),
            _ => q
        };

        return q.Select(a => a.game_id).Distinct().ToList();
    }

    private List<int> SearchDate(SearchQuery query)
    {
        var q = _context.AttributeDates.AsNoTracking().Where(a => a.attribute_name == query.AttributeName);
        DateTime val = DateTime.TryParse(query.AttributeValue, out var d) ? d : DateTime.MinValue;
        DateTime val2 = DateTime.TryParse(query.AttributeValue2, out var d2) ? d2 : DateTime.MinValue;

        q = query.SearchType switch
        {
            SearchType.Equals => q.Where(a => a.attribute_value == val),
            SearchType.NotEquals => q.Where(a => a.attribute_value != val),
            SearchType.Before => q.Where(a => a.attribute_value < val),
            SearchType.After => q.Where(a => a.attribute_value > val),
            SearchType.Between => q.Where(a => a.attribute_value >= val && a.attribute_value <= val2),
            _ => q
        };

        return q.Select(a => a.game_id).Distinct().ToList();
    }

    private Dictionary<int, string> GetAttributeValuesBatch(List<int> gameIds, string attributeName, AttributeTableType tableType)
    {
        if (gameIds.Count == 0) return new Dictionary<int, string>();

        return tableType switch
        {
            AttributeTableType.Text => _context.AttributeTexts
                .AsNoTracking()
                .Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName)
                .ToDictionary(a => a.game_id, a => a.attribute_value),

            AttributeTableType.Number => _context.AttributeNumbers
                .AsNoTracking()
                .Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName)
                .ToDictionary(a => a.game_id, a => a.attribute_value.ToString()),

            AttributeTableType.Boolean => _context.AttributeBooleans
                .AsNoTracking()
                .Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName)
                .ToDictionary(a => a.game_id, a => a.attribute_value.ToString()),

            AttributeTableType.Date => _context.AttributeDates
                .AsNoTracking()
                .Where(a => gameIds.Contains(a.game_id) && a.attribute_name == attributeName)
                .ToDictionary(a => a.game_id, a => a.attribute_value.ToString("yyyy-MM-dd")),

            _ => new Dictionary<int, string>()
        };
    }
}