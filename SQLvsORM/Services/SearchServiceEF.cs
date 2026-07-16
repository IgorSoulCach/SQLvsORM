using Microsoft.EntityFrameworkCore;
using SQLvsORM.Model;

namespace SQLvsORM.Services;

public enum SearchTypeEF
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

public class SearchServiceEF
{
    private readonly GameDbContext _context;

    public SearchServiceEF(GameDbContext context)
    {
        _context = context;
    }

    public List<Dictionary<string, object?>> Search(string attributeName, string attributeValue, string attributeValue2, SearchTypeEF searchType)
    {
        var result = new List<Game>();

        switch (searchType)
        {
            case SearchTypeEF.Equals:
                result = _context.Games
                    .Include(g => g.AttributeTexts)
                    .Include(g => g.AttributeNumbers)
                    .Include(g => g.AttributeBooleans)
                    .Include(g => g.AttributeDates)
                    .Where(g =>
                        g.AttributeTexts.Any(a => a.attribute_name == attributeName && a.attribute_value == attributeValue) ||
                        g.AttributeNumbers.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString() == attributeValue) ||
                        g.AttributeBooleans.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString().ToLower() == attributeValue.ToLower()) ||
                        g.AttributeDates.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString() == attributeValue)
                    )
                    .OrderBy(g => g.title)
                    .ToList();
                break;

            case SearchTypeEF.NotEquals:
                result = _context.Games
                    .Include(g => g.AttributeTexts)
                    .Include(g => g.AttributeNumbers)
                    .Include(g => g.AttributeBooleans)
                    .Include(g => g.AttributeDates)
                    .Where(g =>
                        g.AttributeTexts.Any(a => a.attribute_name == attributeName && a.attribute_value != attributeValue) ||
                        g.AttributeNumbers.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString() != attributeValue) ||
                        g.AttributeBooleans.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString().ToLower() != attributeValue.ToLower()) ||
                        g.AttributeDates.Any(a => a.attribute_name == attributeName && a.attribute_value.ToString() != attributeValue)
                    )
                    .OrderBy(g => g.title)
                    .ToList();
                break;

            case SearchTypeEF.GreaterThan:
                if (decimal.TryParse(attributeValue, out decimal greaterVal))
                {
                    result = _context.Games
                        .Where(g => g.AttributeNumbers.Any(a => a.attribute_name == attributeName && a.attribute_value > greaterVal))
                        .OrderBy(g => g.title)
                        .ToList();
                }
                break;

            case SearchTypeEF.LessThan:
                if (decimal.TryParse(attributeValue, out decimal lessVal))
                {
                    result = _context.Games
                        .Where(g => g.AttributeNumbers.Any(a => a.attribute_name == attributeName && a.attribute_value < lessVal))
                        .OrderBy(g => g.title)
                        .ToList();
                }
                break;

            case SearchTypeEF.Between:
                if (decimal.TryParse(attributeValue, out decimal minVal) && decimal.TryParse(attributeValue2, out decimal maxVal))
                {
                    result = _context.Games
                        .Where(g => g.AttributeNumbers.Any(a => a.attribute_name == attributeName && a.attribute_value >= minVal && a.attribute_value <= maxVal))
                        .OrderBy(g => g.title)
                        .ToList();
                }
                break;

            case SearchTypeEF.Contains:
                result = _context.Games
                    .Where(g => g.AttributeTexts.Any(a => a.attribute_name == attributeName && a.attribute_value.Contains(attributeValue)))
                    .OrderBy(g => g.title)
                    .ToList();
                break;

            case SearchTypeEF.In:
                var values = attributeValue.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
                result = _context.Games
                    .Where(g => g.AttributeTexts.Any(a => a.attribute_name == attributeName && values.Contains(a.attribute_value)))
                    .OrderBy(g => g.title)
                    .ToList();
                break;

            case SearchTypeEF.Before:
                if (DateTime.TryParse(attributeValue, out DateTime beforeDate))
                {
                    result = _context.Games
                        .Where(g => g.AttributeDates.Any(a => a.attribute_name == attributeName && a.attribute_value < beforeDate))
                        .OrderBy(g => g.title)
                        .ToList();
                }
                break;

            case SearchTypeEF.After:
                if (DateTime.TryParse(attributeValue, out DateTime afterDate))
                {
                    result = _context.Games
                        .Where(g => g.AttributeDates.Any(a => a.attribute_name == attributeName && a.attribute_value > afterDate))
                        .OrderBy(g => g.title)
                        .ToList();
                }
                break;
        }

        return result.Select(g => new Dictionary<string, object?>
        {
            ["game_id"] = g.game_id,
            ["title"] = g.title,
            ["release_date"] = g.release_date,
            ["developer"] = g.developer,
            ["publisher"] = g.publisher,
            ["base_price"] = g.base_price
        }).ToList();
    }

    public List<Dictionary<string, object?>> GetAllGames()
    {
        return _context.Games
            .OrderBy(g => g.title)
            .Select(g => new
            {
                g.game_id,
                g.title,
                g.release_date,
                g.developer,
                g.publisher,
                g.base_price
            })
            .AsEnumerable()
            .Select(g => new Dictionary<string, object?>
            {
                ["game_id"] = g.game_id,
                ["title"] = g.title,
                ["release_date"] = g.release_date,
                ["developer"] = g.developer,
                ["publisher"] = g.publisher,
                ["base_price"] = g.base_price
            }).ToList();
    }

    public Dictionary<string, object?>? GetGameById(int id)
    {
        var game = _context.Games.FirstOrDefault(g => g.game_id == id);
        if (game == null) return null;

        return new Dictionary<string, object?>
        {
            ["game_id"] = game.game_id,
            ["title"] = game.title,
            ["release_date"] = game.release_date,
            ["developer"] = game.developer,
            ["publisher"] = game.publisher,
            ["base_price"] = game.base_price
        };
    }

    public Dictionary<string, List<string>> GetAllAttributes()
    {
        return new Dictionary<string, List<string>>
        {
            ["text"] = _context.AttributeTexts.Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["number"] = _context.AttributeNumbers.Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["boolean"] = _context.AttributeBooleans.Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList(),
            ["date"] = _context.AttributeDates.Select(a => a.attribute_name).Distinct().OrderBy(n => n).ToList()
        };
    }

    public string? GetAttributeValue(int gameId, string attributeName)
    {
        var game = _context.Games
            .Include(g => g.AttributeTexts)
            .Include(g => g.AttributeNumbers)
            .Include(g => g.AttributeBooleans)
            .Include(g => g.AttributeDates)
            .FirstOrDefault(g => g.game_id == gameId);

        if (game == null) return null;

        return game.AttributeTexts?.FirstOrDefault(a => a.attribute_name == attributeName)?.attribute_value
            ?? game.AttributeNumbers?.FirstOrDefault(a => a.attribute_name == attributeName)?.attribute_value.ToString()
            ?? game.AttributeBooleans?.FirstOrDefault(a => a.attribute_name == attributeName)?.attribute_value.ToString()
            ?? game.AttributeDates?.FirstOrDefault(a => a.attribute_name == attributeName)?.attribute_value.ToString("yyyy-MM-dd");
    }
}