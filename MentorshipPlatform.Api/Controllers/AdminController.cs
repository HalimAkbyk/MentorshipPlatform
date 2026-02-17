namespace MentorshipPlatform.Api.Controllers;

using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MentorshipPlatform.Application.Admin.Commands.PublishMentor;
using MentorshipPlatform.Application.Admin.Queries.GetPendingMentors;
using MentorshipPlatform.Application.Admin.Queries.GetProcessHistory;
using MentorshipPlatform.Application.Admin.Queries.GetAdminDashboard;
using MentorshipPlatform.Application.Admin.Queries.GetSystemHealth;
using MentorshipPlatform.Application.Admin.Queries.GetPlatformRevenue;
using MentorshipPlatform.Application.Admin.Queries.GetPlatformTransactions;
using MentorshipPlatform.Application.Bookings.Commands.ResolveDispute;
using MentorshipPlatform.Application.Messages.Queries.GetMessageReports;
using MentorshipPlatform.Application.Messages.Commands.ReviewMessageReport;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly IMediator _mediator;
    private readonly IStorageService _storage;

    public AdminController(ApplicationDbContext db, IPaymentService paymentService, IMediator mediator, IStorageService storage)
    {
        _db = db;
        _paymentService = paymentService;
        _mediator = mediator;
        _storage = storage;
    }

    // -----------------------------
    // VERIFICATIONS
    // -----------------------------
    public record VerificationDecisionRequest(string? Notes);

    public record PendingVerificationDto(
        Guid Id,
        Guid MentorUserId,
        string? MentorName,
        string Type,
        string Status,
        string? DocumentUrl,
        DateTime CreatedAt
    );

    [HttpGet("verifications")]
    public async Task<ActionResult<List<PendingVerificationDto>>> GetVerifications([FromQuery] string? status = "Pending")
    {
        var q = _db.MentorVerifications.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<VerificationStatus>(status, true, out var st))
        {
            q = q.Where(v => v.Status == st);
        }

        // Users tablosu ile MentorUserId üzerinden join
        var items = await (
            from v in q
            join u in _db.Users.AsNoTracking() on v.MentorUserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby v.CreatedAt descending
            select new PendingVerificationDto(
                v.Id,
                v.MentorUserId,
                u != null ? u.DisplayName : null,
                v.Type.ToString(),
                v.Status.ToString(),
                v.DocumentUrl,
                v.CreatedAt
            )
        ).ToListAsync();

        return Ok(items);
    }

    [HttpPost("verifications/{id:guid}/approve")]
    public async Task<IActionResult> ApproveVerification([FromRoute] Guid id, [FromBody] VerificationDecisionRequest req)
    {
        var verification = await _db.MentorVerifications.FirstOrDefaultAsync(x => x.Id == id);
        if (verification == null) return NotFound();

        verification.Approve(req.Notes);

        // ✅ Belge onaylandığında mentor'ü otomatik yayınla (IsListed = true)
        var mentorProfile = await _db.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == verification.MentorUserId);

        if (mentorProfile != null && !mentorProfile.IsListed)
        {
            mentorProfile.Publish();
        }

        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("verifications/{id:guid}/reject")]
    public async Task<IActionResult> RejectVerification([FromRoute] Guid id, [FromBody] VerificationDecisionRequest req)
    {
        var verification = await _db.MentorVerifications.FirstOrDefaultAsync(x => x.Id == id);
        if (verification == null) return NotFound();

        // entity Reject(string notes) bekliyor; boş/null göndermeyi engelle
        var notes = string.IsNullOrWhiteSpace(req.Notes) ? "Rejected by admin." : req.Notes!;
        verification.Reject(notes);

        await _db.SaveChangesAsync();
        return Ok();
    }

    // -----------------------------
    // REFUNDS (Order + Booking üzerinden “Pending” üretim)
    // -----------------------------
    public record RefundDecisionRequest(string? Reason);

    public record PendingRefundDto(
        Guid Id,               // OrderId
        Guid? BookingId,        // Order.ResourceId
        Guid? RequesterUserId,  // Order.BuyerUserId
        decimal Amount,
        string Currency,
        string Status,          // "Pending"
        DateTime CreatedAt);

    [HttpGet("refunds")]
    public async Task<ActionResult<List<PendingRefundDto>>> GetRefunds([FromQuery] string? status = "Pending")
    {
        // Bizde RefundRequest entity’si yok.
        // "Pending refund" = Booking iptal olmuş + ilgili Order Paid + henüz Refunded değil.
        // Order.ResourceId = BookingId (OrderType.Booking)

        var items = await (
            from o in _db.Orders.AsNoTracking()
            join b in _db.Bookings.AsNoTracking() on o.ResourceId equals b.Id
            where o.Type == OrderType.Booking
                  && o.Status == OrderStatus.Paid
                  && b.Status == BookingStatus.Cancelled
            orderby o.CreatedAt descending
            select new PendingRefundDto(
                o.Id,
                o.ResourceId,
                o.BuyerUserId,
                o.AmountTotal,
                o.Currency,
                "Pending",
                o.CreatedAt
            )
        ).ToListAsync();

        return Ok(items);
    }

    [HttpPost("refunds/{orderId:guid}/approve")]
    public async Task<IActionResult> ApproveRefund([FromRoute] Guid orderId, [FromBody] RefundDecisionRequest _)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId);
        if (order == null) return NotFound();

        if (order.Type != OrderType.Booking)
            return BadRequest(new { errors = new[] { "Only Booking orders can be refunded via this endpoint." } });

        if (order.Status != OrderStatus.Paid)
            return BadRequest(new { errors = new[] { "Order is not in Paid status." } });

        if (string.IsNullOrWhiteSpace(order.ProviderPaymentId))
            return BadRequest(new { errors = new[] { "ProviderPaymentId is missing on Order." } });

        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == order.ResourceId);
        if (booking == null) return BadRequest(new { errors = new[] { "Related booking not found." } });

        if (booking.Status != BookingStatus.Cancelled)
            return BadRequest(new { errors = new[] { "Only cancelled bookings can be refunded." } });

        var refundRate = booking.CalculateRefundPercentage();
        var refundAmount = order.AmountTotal * refundRate;

        if (refundAmount <= 0)
            return BadRequest(new { errors = new[] { "Refund amount is 0 for this booking." } });

        var result = await _paymentService.RefundPaymentAsync(order.ProviderPaymentId, refundAmount);

        if (!result.Success)
            return BadRequest(new { errors = new[] { result.ErrorMessage ?? "Refund failed." } });

        order.MarkAsRefunded();
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("refunds/{orderId:guid}/reject")]
    public async Task<IActionResult> RejectRefund([FromRoute] Guid orderId, [FromBody] RefundDecisionRequest _)
    {
        // Sistemde refund request state’i yok. Bu yüzden “reject” sadece 200 döner.
        // İstersen ileride RefundRequest tablosu ekleyip burada status güncelleriz.
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId);
        if (order == null) return NotFound();

        return Ok();
    }

    // -----------------------------
    // USERS (Suspend / Unsuspend)
    // -----------------------------
    public record SuspendUserRequest(string Reason);

    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<IActionResult> SuspendUser([FromRoute] Guid userId, [FromBody] SuspendUserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null) return NotFound();

        user.Suspend();
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("users/{userId:guid}/unsuspend")]
    public async Task<IActionResult> UnsuspendUser([FromRoute] Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user == null) return NotFound();

        user.Activate();
        await _db.SaveChangesAsync();

        return Ok();
    }
    // -----------------------------
// PENDING MENTORS
// -----------------------------

    [HttpGet("pending-mentors")]
    public async Task<IActionResult> GetPendingMentors()
    {
        var result = await _mediator.Send(new GetPendingMentorsQuery());
    
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    [HttpPost("mentors/{userId:guid}/publish")]
    public async Task<IActionResult> PublishMentor([FromRoute] Guid userId)
    {
        var result = await _mediator.Send(new PublishMentorCommand(userId));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { success = true });
    }

    [HttpPost("mentors/{userId:guid}/unpublish")]
    public async Task<IActionResult> UnpublishMentor([FromRoute] Guid userId)
    {
        var mentorProfile = await _db.MentorProfiles
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (mentorProfile == null)
            return NotFound(new { errors = new[] { "Mentor profile not found" } });

        mentorProfile.Unpublish();
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // -----------------------------
    // ALL BOOKINGS (Admin Calendar)
    // -----------------------------
    public record AdminBookingDto(
        Guid Id,
        Guid StudentUserId,
        string? StudentName,
        Guid MentorUserId,
        string? MentorName,
        DateTime StartAt,
        DateTime EndAt,
        int DurationMin,
        string Status,
        decimal Price,
        string Currency,
        string? OfferingTitle);

    [HttpGet("bookings")]
    public async Task<IActionResult> GetAllBookings(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] Guid? mentorUserId)
    {
        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Student)
            .Include(b => b.Mentor)
            .Include(b => b.Offering)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(b => b.StartAt >= from.Value);
        if (to.HasValue)
            query = query.Where(b => b.EndAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var st))
            query = query.Where(b => b.Status == st);
        if (mentorUserId.HasValue)
            query = query.Where(b => b.MentorUserId == mentorUserId.Value);

        var items = await query
            .OrderByDescending(b => b.StartAt)
            .Select(b => new AdminBookingDto(
                b.Id,
                b.StudentUserId,
                b.Student != null ? b.Student.DisplayName : null,
                b.MentorUserId,
                b.Mentor != null ? b.Mentor.DisplayName : null,
                b.StartAt,
                b.EndAt,
                b.DurationMin,
                b.Status.ToString(),
                b.Offering != null ? b.Offering.PriceAmount : 0,
                b.Offering != null ? b.Offering.Currency : "TRY",
                b.Offering != null ? b.Offering.Title : null))
            .ToListAsync();

        return Ok(items);
    }

    // -----------------------------
    // PROCESS HISTORY (Audit Log)
    // -----------------------------
    [HttpGet("process-history")]
    public async Task<IActionResult> GetProcessHistory(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? action,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetProcessHistoryQuery(
            entityType, entityId, action, dateFrom, dateTo, page, pageSize));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    // -----------------------------
    // ADMIN DASHBOARD
    // -----------------------------
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetAdminDashboardQuery());

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    // -----------------------------
    // SYSTEM HEALTH DASHBOARD
    // -----------------------------
    [HttpGet("system-health")]
    public async Task<IActionResult> GetSystemHealth()
    {
        var result = await _mediator.Send(new GetSystemHealthQuery());

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Data);
    }

    // -----------------------------
    // DISPUTES
    // -----------------------------
    public record DisputeDto(
        Guid BookingId,
        Guid StudentUserId,
        string? StudentName,
        Guid MentorUserId,
        string? MentorName,
        string? Reason,
        DateTime StartAt,
        DateTime CreatedAt);

    [HttpGet("disputes")]
    public async Task<IActionResult> GetDisputes()
    {
        var items = await (
            from b in _db.Bookings.AsNoTracking()
            join s in _db.Users.AsNoTracking() on b.StudentUserId equals s.Id into sj
            from s in sj.DefaultIfEmpty()
            join m in _db.Users.AsNoTracking() on b.MentorUserId equals m.Id into mj
            from m in mj.DefaultIfEmpty()
            where b.Status == BookingStatus.Disputed
            orderby b.UpdatedAt descending
            select new DisputeDto(
                b.Id,
                b.StudentUserId,
                s != null ? s.DisplayName : null,
                b.MentorUserId,
                m != null ? m.DisplayName : null,
                b.CancellationReason,
                b.StartAt,
                b.CreatedAt)
        ).ToListAsync();

        return Ok(items);
    }

    public record ResolveDisputeRequest(string Resolution, string? Notes);

    [HttpPost("disputes/{bookingId:guid}/resolve")]
    public async Task<IActionResult> ResolveDispute(
        [FromRoute] Guid bookingId,
        [FromBody] ResolveDisputeRequest request)
    {
        var result = await _mediator.Send(new ResolveDisputeCommand(
            bookingId, request.Resolution, request.Notes));

        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { success = true });
    }

    // -----------------------------
    // REVENUE
    // -----------------------------
    [HttpGet("revenue/summary")]
    public async Task<IActionResult> GetRevenueSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await _mediator.Send(new GetPlatformRevenueSummaryQuery(from, to));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    [HttpGet("revenue/transactions")]
    public async Task<IActionResult> GetRevenueTransactions(
        [FromQuery] string? accountType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPlatformTransactionsQuery(accountType, page, pageSize));
        if (!result.IsSuccess) return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    // -----------------------------
    // PRESET AVATARS
    // -----------------------------
    public record PresetAvatarDto(Guid Id, string Url, string Label, int SortOrder, bool IsActive);
    public record UpdatePresetAvatarRequest(string Label, int SortOrder, bool IsActive);

    [HttpGet("preset-avatars")]
    public async Task<IActionResult> GetPresetAvatars()
    {
        var items = await _db.PresetAvatars
            .AsNoTracking()
            .OrderBy(a => a.SortOrder)
            .Select(a => new PresetAvatarDto(a.Id, a.Url, a.Label, a.SortOrder, a.IsActive))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("preset-avatars")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreatePresetAvatar(
        IFormFile file,
        [FromForm] string label,
        [FromForm] int sortOrder)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya secilmedi." } });

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { errors = new[] { "Dosya boyutu 2MB'den buyuk olamaz." } });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { errors = new[] { "Sadece JPG, PNG, GIF, WebP veya SVG dosyalari yuklenebilir." } });

        using var stream = file.OpenReadStream();
        var result = await _storage.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            "preset-avatars",
            "avatar",
            default);

        if (!result.Success)
            return BadRequest(new { errors = new[] { result.ErrorMessage ?? "Dosya yuklenemedi." } });

        var avatar = new Domain.Entities.PresetAvatar(result.PublicUrl!, label, sortOrder);
        _db.PresetAvatars.Add(avatar);
        await _db.SaveChangesAsync();

        return Ok(new PresetAvatarDto(avatar.Id, avatar.Url, avatar.Label, avatar.SortOrder, avatar.IsActive));
    }

    [HttpPut("preset-avatars/{id:guid}")]
    public async Task<IActionResult> UpdatePresetAvatar([FromRoute] Guid id, [FromBody] UpdatePresetAvatarRequest req)
    {
        var avatar = await _db.PresetAvatars.FirstOrDefaultAsync(a => a.Id == id);
        if (avatar == null) return NotFound();

        avatar.Update(avatar.Url, req.Label, req.SortOrder, req.IsActive);
        await _db.SaveChangesAsync();

        return Ok(new PresetAvatarDto(avatar.Id, avatar.Url, avatar.Label, avatar.SortOrder, avatar.IsActive));
    }

    [HttpPut("preset-avatars/{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdatePresetAvatarImage([FromRoute] Guid id, IFormFile file)
    {
        var avatar = await _db.PresetAvatars.FirstOrDefaultAsync(a => a.Id == id);
        if (avatar == null) return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest(new { errors = new[] { "Dosya secilmedi." } });

        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { errors = new[] { "Dosya boyutu 2MB'den buyuk olamaz." } });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new { errors = new[] { "Sadece JPG, PNG, GIF, WebP veya SVG dosyalari yuklenebilir." } });

        using var stream = file.OpenReadStream();
        var result = await _storage.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            "preset-avatars",
            "avatar",
            default);

        if (!result.Success)
            return BadRequest(new { errors = new[] { result.ErrorMessage ?? "Dosya yuklenemedi." } });

        avatar.Update(result.PublicUrl!, avatar.Label, avatar.SortOrder, avatar.IsActive);
        await _db.SaveChangesAsync();

        return Ok(new PresetAvatarDto(avatar.Id, avatar.Url, avatar.Label, avatar.SortOrder, avatar.IsActive));
    }

    [HttpDelete("preset-avatars/{id:guid}")]
    public async Task<IActionResult> DeletePresetAvatar([FromRoute] Guid id)
    {
        var avatar = await _db.PresetAvatars.FirstOrDefaultAsync(a => a.Id == id);
        if (avatar == null) return NotFound();

        _db.PresetAvatars.Remove(avatar);
        await _db.SaveChangesAsync();

        return Ok();
    }

    // -----------------------------
    // MESSAGE REPORTS
    // -----------------------------

    [HttpGet("message-reports")]
    public async Task<IActionResult> GetMessageReports(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetMessageReportsQuery(status, page, pageSize));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.Data);
    }

    public record ReviewMessageReportRequest(ReportStatus Status, string? AdminNotes);

    [HttpPost("message-reports/{reportId:guid}/review")]
    public async Task<IActionResult> ReviewMessageReport(
        Guid reportId, [FromBody] ReviewMessageReportRequest body)
    {
        var result = await _mediator.Send(new ReviewMessageReportCommand(
            reportId, body.Status, body.AdminNotes));
        if (!result.IsSuccess)
            return BadRequest(new { errors = result.Errors });
        return Ok(new { success = true });
    }
}
