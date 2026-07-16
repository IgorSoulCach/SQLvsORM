using System.Data;
using Microsoft.AspNetCore.Mvc;
using SQLvsORM.Services;

namespace SQLvsORM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly SearchServiceSQL _searchService;
    private readonly SearchServiceEF _searchServiceEF;

    public GamesController(SearchServiceSQL searchService, SearchServiceEF searchServiceEF)
    {
        _searchService = searchService;
        _searchServiceEF = searchServiceEF;
    }

    /// <summary>
    /// Поиск игр по атрибуту
    /// </summary>
    [HttpGet("search")]
    public IActionResult Search(
        [FromQuery] string attributeName,
        [FromQuery] string attributeValue,
        [FromQuery] SearchType searchType = SearchType.Equals,
        [FromQuery] string? attributeValue2 = null)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            return BadRequest(new { error = "Имя атрибута обязательно" });

        if (string.IsNullOrWhiteSpace(attributeValue))
            return BadRequest(new { error = "Значение атрибута обязательно" });

        if (searchType == SearchType.Between && string.IsNullOrWhiteSpace(attributeValue2))
            return BadRequest(new { error = "Для Between нужно второе значение" });

        try
        {
            var result = _searchService.Search(attributeName, attributeValue, attributeValue2 ?? "", searchType);

            var games = new List<Dictionary<string, object?>>();
            foreach (DataRow row in result.Rows)
            {
                var game = new Dictionary<string, object?>
                {
                    ["game_id"] = row["game_id"],
                    ["title"] = row["title"],
                    ["release_date"] = row["release_date"] is DBNull ? null : ((DateTime)row["release_date"]).ToString("yyyy-MM-dd"),
                    ["developer"] = row["developer"],
                    ["publisher"] = row["publisher"],
                    ["base_price"] = row["base_price"] is DBNull ? null : row["base_price"],
                    [$"attr_{attributeName}"] = _searchService.GetAttributeValue(row["game_id"].ToString()!, attributeName)
                };

                games.Add(game);
            }

            var symbols = new Dictionary<SearchType, string>
            {
                { SearchType.Equals, "=" },
                { SearchType.NotEquals, "!=" },
                { SearchType.GreaterThan, ">" },
                { SearchType.LessThan, "<" },
                { SearchType.Between, " - " },
                { SearchType.Contains, " contains " },
                { SearchType.In, " in " },
                { SearchType.Before, " before " },
                { SearchType.After, " after " }
            };

            string symbol = symbols.GetValueOrDefault(searchType, "?");
            string searchDescription = searchType == SearchType.Between
                ? $"{attributeName} {attributeValue} {symbol} {attributeValue2}"
                : $"{attributeName} {symbol} {attributeValue}";

            return Ok(new
            {
                count = games.Count,
                search = searchDescription,
                games
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить все игры
    /// </summary>
    [HttpGet("all")]
    public IActionResult GetAll()
    {
        try
        {
            var games = _searchService.GetAllGames();
            return Ok(new { count = games.Count, games });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить игру по ID
    /// </summary>
    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        try
        {
            var game = _searchService.GetGameById(id);
            if (game == null)
                return NotFound(new { error = $"Игра с ID {id} не найдена" });

            return Ok(game);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить список всех атрибутов
    /// </summary>
    [HttpGet("AllAttributes")]
    public IActionResult GetAllAttributes()
    {
        try
        {
            var attributes = _searchService.GetAllAttributes();
            return Ok(attributes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить значение атрибута игры
    /// </summary>
    [HttpGet("{id}/attribute/{attributeName}")]
    public IActionResult GetAttributeValue(int id, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            return BadRequest(new { error = "Имя атрибута обязательно" });

        try
        {
            var game = _searchService.GetGameById(id);
            if (game == null)
                return NotFound(new { error = $"Игра с ID {id} не найдена" });

            var value = _searchService.GetAttributeValue(id.ToString(), attributeName);

            if (value == null)
                return NotFound(new { error = $"Атрибут '{attributeName}' не найден у игры с ID {id}" });

            return Ok(new
            {
                game_id = id,
                game_title = game["title"],
                attribute_name = attributeName,
                attribute_value = value
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

}