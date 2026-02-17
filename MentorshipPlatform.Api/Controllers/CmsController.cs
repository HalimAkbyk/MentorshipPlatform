using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public class CmsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CmsController(ApplicationDbContext db) => _db = db;

    // ============ HOMEPAGE MODULES ============

    // GET /api/admin/cms/modules
    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/cms/modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _db.HomepageModules
            .AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.ModuleType, m.Title, m.Subtitle, m.Content, m.SortOrder, m.IsActive, m.CreatedAt, m.UpdatedAt })
            .ToListAsync();
        return Ok(modules);
    }

    // POST /api/admin/cms/modules
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("admin/cms/modules")]
    public async Task<IActionResult> CreateModule([FromBody] CreateModuleRequest request)
    {
        var module = HomepageModule.Create(request.ModuleType, request.Title, request.Subtitle, request.Content, request.SortOrder);
        _db.HomepageModules.Add(module);
        await _db.SaveChangesAsync();
        return Ok(new { module.Id });
    }

    // PUT /api/admin/cms/modules/{id}
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("admin/cms/modules/{id:guid}")]
    public async Task<IActionResult> UpdateModule(Guid id, [FromBody] UpdateModuleRequest request)
    {
        var module = await _db.HomepageModules.FindAsync(id);
        if (module == null) return NotFound();
        module.Update(request.Title, request.Subtitle, request.Content, request.SortOrder, request.IsActive);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // PUT /api/admin/cms/modules/reorder
    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("admin/cms/modules/reorder")]
    public async Task<IActionResult> ReorderModules([FromBody] List<ReorderItem> items)
    {
        foreach (var item in items)
        {
            var module = await _db.HomepageModules.FindAsync(item.Id);
            if (module != null) module.SetSortOrder(item.SortOrder);
        }
        await _db.SaveChangesAsync();
        return Ok();
    }

    // DELETE /api/admin/cms/modules/{id}
    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("admin/cms/modules/{id:guid}")]
    public async Task<IActionResult> DeleteModule(Guid id)
    {
        var module = await _db.HomepageModules.FindAsync(id);
        if (module == null) return NotFound();
        _db.HomepageModules.Remove(module);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // GET /api/cms/modules/active (public)
    [AllowAnonymous]
    [HttpGet("cms/modules/active")]
    public async Task<IActionResult> GetActiveModules()
    {
        var modules = await _db.HomepageModules
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.ModuleType, m.Title, m.Subtitle, m.Content, m.SortOrder })
            .ToListAsync();
        return Ok(modules);
    }

    // ============ BANNERS ============

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/cms/banners")]
    public async Task<IActionResult> GetBanners()
    {
        var banners = await _db.Banners
            .AsNoTracking()
            .OrderBy(b => b.SortOrder)
            .Select(b => new { b.Id, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.Position, b.IsActive, b.StartDate, b.EndDate, b.SortOrder, b.CreatedAt })
            .ToListAsync();
        return Ok(banners);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("admin/cms/banners")]
    public async Task<IActionResult> CreateBanner([FromBody] CreateBannerRequest request)
    {
        var banner = Banner.Create(request.Title, request.Description, request.ImageUrl, request.LinkUrl, request.Position, request.StartDate, request.EndDate, request.SortOrder);
        _db.Banners.Add(banner);
        await _db.SaveChangesAsync();
        return Ok(new { banner.Id });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("admin/cms/banners/{id:guid}")]
    public async Task<IActionResult> UpdateBanner(Guid id, [FromBody] UpdateBannerRequest request)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner == null) return NotFound();
        banner.Update(request.Title, request.Description, request.ImageUrl, request.LinkUrl, request.Position, request.StartDate, request.EndDate, request.SortOrder, request.IsActive);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("admin/cms/banners/{id:guid}")]
    public async Task<IActionResult> DeleteBanner(Guid id)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner == null) return NotFound();
        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("cms/banners/active")]
    public async Task<IActionResult> GetActiveBanners()
    {
        var now = DateTime.UtcNow;
        var banners = await _db.Banners
            .AsNoTracking()
            .Where(b => b.IsActive && (b.StartDate == null || b.StartDate <= now) && (b.EndDate == null || b.EndDate >= now))
            .OrderBy(b => b.SortOrder)
            .Select(b => new { b.Id, b.Title, b.Description, b.ImageUrl, b.LinkUrl, b.Position })
            .ToListAsync();
        return Ok(banners);
    }

    // ============ ANNOUNCEMENTS ============

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/cms/announcements")]
    public async Task<IActionResult> GetAnnouncements()
    {
        var items = await _db.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Title, a.Content, a.Type, a.TargetAudience, a.IsActive, a.StartDate, a.EndDate, a.IsDismissible, a.CreatedAt })
            .ToListAsync();
        return Ok(items);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("admin/cms/announcements")]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
    {
        var a = Announcement.Create(request.Title, request.Content, request.Type, request.TargetAudience, request.StartDate, request.EndDate, request.IsDismissible);
        _db.Announcements.Add(a);
        await _db.SaveChangesAsync();
        return Ok(new { a.Id });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("admin/cms/announcements/{id:guid}")]
    public async Task<IActionResult> UpdateAnnouncement(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a == null) return NotFound();
        a.Update(request.Title, request.Content, request.Type, request.TargetAudience, request.StartDate, request.EndDate, request.IsDismissible, request.IsActive);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("admin/cms/announcements/{id:guid}")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a == null) return NotFound();
        _db.Announcements.Remove(a);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("cms/announcements/active")]
    public async Task<IActionResult> GetActiveAnnouncements()
    {
        var now = DateTime.UtcNow;
        var items = await _db.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive && (a.StartDate == null || a.StartDate <= now) && (a.EndDate == null || a.EndDate >= now))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Title, a.Content, a.Type, a.TargetAudience, a.IsDismissible })
            .ToListAsync();
        return Ok(items);
    }

    // ============ STATIC PAGES ============

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/cms/pages")]
    public async Task<IActionResult> GetPages()
    {
        var pages = await _db.StaticPages
            .AsNoTracking()
            .OrderBy(p => p.Title)
            .Select(p => new { p.Id, p.Slug, p.Title, p.MetaTitle, p.MetaDescription, p.IsPublished, p.CreatedAt, p.UpdatedAt })
            .ToListAsync();
        return Ok(pages);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("admin/cms/pages")]
    public async Task<IActionResult> CreatePage([FromBody] CreatePageRequest request)
    {
        var existing = await _db.StaticPages.AnyAsync(p => p.Slug == request.Slug);
        if (existing) return BadRequest(new { error = "Bu slug zaten kullanımda" });
        var page = StaticPage.Create(request.Slug, request.Title, request.Content, request.MetaTitle, request.MetaDescription);
        _db.StaticPages.Add(page);
        await _db.SaveChangesAsync();
        return Ok(new { page.Id });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/cms/pages/{id:guid}")]
    public async Task<IActionResult> GetPage(Guid id)
    {
        var page = await _db.StaticPages.FindAsync(id);
        if (page == null) return NotFound();
        return Ok(new { page.Id, page.Slug, page.Title, page.Content, page.MetaTitle, page.MetaDescription, page.IsPublished, page.CreatedAt, page.UpdatedAt });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("admin/cms/pages/{id:guid}")]
    public async Task<IActionResult> UpdatePage(Guid id, [FromBody] UpdatePageRequest request)
    {
        var page = await _db.StaticPages.FindAsync(id);
        if (page == null) return NotFound();
        page.Update(request.Title, request.Content, request.MetaTitle, request.MetaDescription, request.IsPublished);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("admin/cms/pages/{id:guid}")]
    public async Task<IActionResult> DeletePage(Guid id)
    {
        var page = await _db.StaticPages.FindAsync(id);
        if (page == null) return NotFound();
        _db.StaticPages.Remove(page);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("cms/pages/{slug}")]
    public async Task<IActionResult> GetPageBySlug(string slug)
    {
        var page = await _db.StaticPages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);
        if (page == null) return NotFound();
        return Ok(new { page.Id, page.Slug, page.Title, page.Content, page.MetaTitle, page.MetaDescription });
    }

    // ============ REQUEST RECORDS ============

    public record CreateModuleRequest(string ModuleType, string Title, string? Subtitle, string? Content, int SortOrder);
    public record UpdateModuleRequest(string Title, string? Subtitle, string? Content, int SortOrder, bool IsActive);
    public record ReorderItem(Guid Id, int SortOrder);
    public record CreateBannerRequest(string Title, string? Description, string? ImageUrl, string? LinkUrl, string Position, DateTime? StartDate, DateTime? EndDate, int SortOrder);
    public record UpdateBannerRequest(string Title, string? Description, string? ImageUrl, string? LinkUrl, string Position, DateTime? StartDate, DateTime? EndDate, int SortOrder, bool IsActive);
    public record CreateAnnouncementRequest(string Title, string Content, string Type, string TargetAudience, DateTime? StartDate, DateTime? EndDate, bool IsDismissible);
    public record UpdateAnnouncementRequest(string Title, string Content, string Type, string TargetAudience, DateTime? StartDate, DateTime? EndDate, bool IsDismissible, bool IsActive);
    public record CreatePageRequest(string Slug, string Title, string Content, string? MetaTitle, string? MetaDescription);
    public record UpdatePageRequest(string Title, string Content, string? MetaTitle, string? MetaDescription, bool IsPublished);
}
