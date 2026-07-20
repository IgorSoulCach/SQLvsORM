using Microsoft.AspNetCore.Mvc;
using SQLvsORM.Models;
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

    [HttpPost("search")]
    public IActionResult Search([FromBody] SearchQuery query)
    {
        var result = _searchService.Search(query);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpPost("search-ef")]
    public IActionResult SearchEF([FromBody] SearchQuery query)
    {
        var result = _searchServiceEF.Search(query);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        var result = _searchService.GetGameById(id);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }
}