using Microsoft.AspNetCore.Mvc;
using SQLvsORM.Models;
using SQLvsORM.Services;

namespace SQLvsORM.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly SearchServiceSQL _searchService;

    public GamesController(SearchServiceSQL searchService)
    {
        _searchService = searchService;
    }
    [HttpPost("search")]
    public IActionResult Search([FromBody] SearchQuery query)
    {
        var result = _searchService.Search(query.AttributeName, query.AttributeValue, query.AttributeValue2, query.SearchType);
        return result.IsSuccess
            ? Ok(result.Data)
            : BadRequest(result.Error);
    }

    [HttpGet("all")]
    public IActionResult GetAll()
    {
        var result = _searchService.GetAllGames();
        return result.IsSuccess
            ? Ok(result.Data)
            : BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var result = _searchService.GetGameById(id);
        return result.IsSuccess
            ? Ok(result.Data)
            : NotFound(result.Error);
    }

    [HttpGet("AllAttributes")]
    public IActionResult GetAllAttributes()
    {
        var result = _searchService.GetAllAttributes();
        return Ok(result);
    }

    [HttpGet("{id}/attribute/{attributeName}")]
    public IActionResult GetAttributeValue(int id, string attributeName)
    {
        var result = _searchService.GetAttributeValue(id.ToString(), attributeName);
        return Ok(result);
    }
}