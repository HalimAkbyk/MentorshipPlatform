namespace MentorshipPlatform.Api.Controllers;

using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CategoriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/categories — public, returns active categories sorted by SortOrder
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<CategoryDto>>> GetActiveCategories([FromQuery] string? entityType = null)
    {
        var query = _db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(c => c.EntityType == entityType);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                EntityType = c.EntityType,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }
}
