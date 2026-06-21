using AsyncApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Controllers;

[ApiController]
[Route("categories")]
public sealed class CategoriesController(AsyncApiDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await db.Categories
            .Where(c => c.Active)
            .OrderBy(c => c.Title)
            .Select(c => new { c.Id, c.Title, c.Description })
            .ToListAsync();

        return Ok(categories);
    }
}
