using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Application.Admin.Queries.GetAllUsers;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using MentorshipPlatform.Persistence;

namespace MentorshipPlatform.Api.Controllers;

// ──────────── DTOs ────────────

public class CouponDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public string CreatedByRole { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetUserId { get; set; }
    public string? TargetProductType { get; set; }
    public Guid? TargetProductId { get; set; }
    public Guid? MentorUserId { get; set; }
    public int? MaxUsageCount { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public int CurrentUsageCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CouponUsageDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid OrderId { get; set; }
    public decimal DiscountApplied { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public string TargetType { get; set; } = "All";
    public Guid? TargetUserId { get; set; }
    public string? TargetProductType { get; set; }
    public Guid? TargetProductId { get; set; }
    public int? MaxUsageCount { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpdateCouponRequest
{
    public string? Description { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUsageCount { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ValidateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? MentorUserId { get; set; }
}

public class ValidateCouponResponse
{
    public bool Valid { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal EstimatedDiscount { get; set; }
    public string Message { get; set; } = "";
}

public class ApplyCouponRequest
{
    public Guid OrderId { get; set; }
    public string Code { get; set; } = string.Empty;
}

// ──────────── Controller ────────────

[ApiController]
[Route("api/coupons")]
public class CouponsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CouponsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ADMIN ENDPOINTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// List all coupons (Admin only) — paginated with filters
    /// </summary>
    [HttpGet("/api/admin/coupons")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminListCoupons(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Coupons.AsNoTracking().AsQueryable();

        // Status filter
        if (!string.IsNullOrEmpty(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(c => c.IsActive);
            else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                query = query.Where(c => !c.IsActive);
        }

        // Type filter (Percentage / FixedAmount)
        if (!string.IsNullOrEmpty(type))
            query = query.Where(c => c.DiscountType == type);

        // Search filter (code or description)
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(searchLower) ||
                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                Description = c.Description,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MaxDiscountAmount = c.MaxDiscountAmount,
                MinOrderAmount = c.MinOrderAmount,
                CreatedByRole = c.CreatedByRole,
                TargetType = c.TargetType,
                TargetUserId = c.TargetUserId,
                TargetProductType = c.TargetProductType,
                TargetProductId = c.TargetProductId,
                MentorUserId = c.MentorUserId,
                MaxUsageCount = c.MaxUsageCount,
                MaxUsagePerUser = c.MaxUsagePerUser,
                CurrentUsageCount = c.CurrentUsageCount,
                IsActive = c.IsActive,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<CouponDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Create a coupon (Admin)
    /// </summary>
    [HttpPost("/api/admin/coupons")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminCreateCoupon([FromBody] CreateCouponRequest request)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError != null) return BadRequest(new { errors = new[] { validationError } });

        // Check code uniqueness
        var codeUpper = request.Code.ToUpper().Trim();
        var exists = await _db.Coupons.AnyAsync(c => c.Code == codeUpper);
        if (exists)
            return BadRequest(new { errors = new[] { "A coupon with this code already exists." } });

        var adminId = _currentUser.UserId!.Value;

        var coupon = Coupon.Create(
            request.Code,
            request.Description,
            request.DiscountType,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            adminId,
            "Admin",
            request.TargetType,
            request.TargetUserId,
            request.TargetProductType,
            request.TargetProductId,
            null, // MentorUserId — admin coupons are not mentor-scoped
            request.MaxUsageCount,
            request.MaxUsagePerUser,
            request.StartDate,
            request.EndDate);

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();

        return Ok(new { id = coupon.Id, code = coupon.Code });
    }

    /// <summary>
    /// Update a coupon (Admin)
    /// </summary>
    [HttpPut("/api/admin/coupons/{id:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminUpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        coupon.Update(
            request.Description,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            request.MaxUsageCount,
            request.MaxUsagePerUser,
            request.StartDate,
            request.EndDate);

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Activate a coupon (Admin)
    /// </summary>
    [HttpPost("/api/admin/coupons/{id:guid}/activate")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminActivateCoupon(Guid id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        coupon.Activate();
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deactivate a coupon (Admin)
    /// </summary>
    [HttpPost("/api/admin/coupons/{id:guid}/deactivate")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminDeactivateCoupon(Guid id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        coupon.Deactivate();
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Delete a coupon (Admin) — only if never used
    /// </summary>
    [HttpDelete("/api/admin/coupons/{id:guid}")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminDeleteCoupon(Guid id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        if (coupon.CurrentUsageCount > 0)
            return BadRequest(new { errors = new[] { "Cannot delete a coupon that has been used. Deactivate it instead." } });

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// List usages for a specific coupon (Admin)
    /// </summary>
    [HttpGet("/api/admin/coupons/{id:guid}/usages")]
    [Authorize(Policy = "RequireAdminRole")]
    public async Task<IActionResult> AdminGetCouponUsages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        var query = _db.CouponUsages.AsNoTracking()
            .Where(cu => cu.CouponId == id);

        var totalCount = await query.CountAsync();

        var usages = await query
            .OrderByDescending(cu => cu.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cu => new
            {
                cu.Id,
                cu.UserId,
                cu.OrderId,
                cu.DiscountApplied,
                cu.CreatedAt
            })
            .ToListAsync();

        // Resolve user names
        var userIds = usages.Select(u => u.UserId).Distinct().ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var items = usages.Select(u => new CouponUsageDto
        {
            Id = u.Id,
            UserId = u.UserId,
            UserName = userNames.GetValueOrDefault(u.UserId, "Unknown"),
            OrderId = u.OrderId,
            DiscountApplied = u.DiscountApplied,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Ok(new PagedResult<CouponUsageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  MENTOR ENDPOINTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// List mentor's own coupons
    /// </summary>
    [HttpGet("my-coupons")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> MentorListCoupons(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var mentorId = _currentUser.UserId!.Value;

        var query = _db.Coupons.AsNoTracking()
            .Where(c => c.MentorUserId == mentorId);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                Description = c.Description,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MaxDiscountAmount = c.MaxDiscountAmount,
                MinOrderAmount = c.MinOrderAmount,
                CreatedByRole = c.CreatedByRole,
                TargetType = c.TargetType,
                TargetUserId = c.TargetUserId,
                TargetProductType = c.TargetProductType,
                TargetProductId = c.TargetProductId,
                MentorUserId = c.MentorUserId,
                MaxUsageCount = c.MaxUsageCount,
                MaxUsagePerUser = c.MaxUsagePerUser,
                CurrentUsageCount = c.CurrentUsageCount,
                IsActive = c.IsActive,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<CouponDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Create a coupon (Mentor) — scoped to mentor's own products
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> MentorCreateCoupon([FromBody] CreateCouponRequest request)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError != null) return BadRequest(new { errors = new[] { validationError } });

        // Check code uniqueness
        var codeUpper = request.Code.ToUpper().Trim();
        var exists = await _db.Coupons.AnyAsync(c => c.Code == codeUpper);
        if (exists)
            return BadRequest(new { errors = new[] { "A coupon with this code already exists." } });

        var mentorId = _currentUser.UserId!.Value;

        var coupon = Coupon.Create(
            request.Code,
            request.Description,
            request.DiscountType,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            mentorId,
            "Mentor",
            request.TargetType,
            request.TargetUserId,
            request.TargetProductType,
            request.TargetProductId,
            mentorId, // MentorUserId — scoped to this mentor
            request.MaxUsageCount,
            request.MaxUsagePerUser,
            request.StartDate,
            request.EndDate);

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();

        return Ok(new { id = coupon.Id, code = coupon.Code });
    }

    /// <summary>
    /// Update a coupon (Mentor — own only)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> MentorUpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
    {
        var mentorId = _currentUser.UserId!.Value;
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id && c.MentorUserId == mentorId);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        coupon.Update(
            request.Description,
            request.DiscountValue,
            request.MaxDiscountAmount,
            request.MinOrderAmount,
            request.MaxUsageCount,
            request.MaxUsagePerUser,
            request.StartDate,
            request.EndDate);

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Deactivate a coupon (Mentor — own only)
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "RequireMentorRole")]
    public async Task<IActionResult> MentorDeactivateCoupon(Guid id)
    {
        var mentorId = _currentUser.UserId!.Value;
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id && c.MentorUserId == mentorId);
        if (coupon == null)
            return NotFound(new { errors = new[] { "Coupon not found." } });

        coupon.Deactivate();
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ═══════════════════════════════════════════════════════════════
    //  PUBLIC / AUTHENTICATED ENDPOINTS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Validate a coupon code — checks all constraints and returns estimated discount
    /// </summary>
    [HttpPost("validate")]
    [Authorize]
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request)
    {
        var userId = _currentUser.UserId!.Value;
        var (valid, message, coupon, discount) = await ValidateCouponInternal(
            request.Code, userId, request.ProductType, request.ProductId, request.MentorUserId, 0);

        if (!valid || coupon == null)
            return Ok(new ValidateCouponResponse { Valid = false, Message = message });

        return Ok(new ValidateCouponResponse
        {
            Valid = true,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            EstimatedDiscount = discount,
            Message = message
        });
    }

    /// <summary>
    /// Apply a coupon to a pending order
    /// </summary>
    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponRequest request)
    {
        var userId = _currentUser.UserId!.Value;

        // Find the order
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId);
        if (order == null)
            return NotFound(new { errors = new[] { "Order not found." } });

        if (order.BuyerUserId != userId)
            return Forbid();

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { errors = new[] { "Coupon can only be applied to pending orders." } });

        if (!string.IsNullOrEmpty(order.CouponCode))
            return BadRequest(new { errors = new[] { "A coupon has already been applied to this order." } });

        // Determine product type and mentor from order
        string productType = order.Type.ToString();
        Guid? mentorUserId = await ResolveMentorForOrder(order);

        // Validate coupon
        var (valid, message, coupon, discount) = await ValidateCouponInternal(
            request.Code, userId, productType, order.ResourceId, mentorUserId, order.AmountTotal);

        if (!valid || coupon == null)
            return BadRequest(new { errors = new[] { message } });

        // Calculate final discount based on actual order amount
        var finalDiscount = coupon.CalculateDiscount(order.AmountTotal);
        if (finalDiscount <= 0)
            return BadRequest(new { errors = new[] { "Coupon does not provide any discount for this order amount." } });

        // Apply coupon to order (include who created the coupon for ledger calculation)
        order.ApplyCoupon(coupon.Code, finalDiscount, coupon.CreatedByRole);

        // Create usage record
        var usage = CouponUsage.Create(coupon.Id, userId, order.Id, finalDiscount);
        _db.CouponUsages.Add(usage);

        // Increment coupon usage count
        coupon.IncrementUsage();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            couponCode = coupon.Code,
            discountAmount = finalDiscount,
            originalAmount = order.AmountTotal,
            finalAmount = order.AmountTotal - finalDiscount
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task<(bool valid, string message, Coupon? coupon, decimal discount)> ValidateCouponInternal(
        string code, Guid userId, string productType, Guid productId, Guid? mentorUserId, decimal orderAmount)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "Coupon code is required.", null, 0);

        var codeUpper = code.ToUpper().Trim();

        // 1. Find coupon by code (case-insensitive)
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == codeUpper);
        if (coupon == null)
            return (false, "Invalid coupon code.", null, 0);

        // 2. Check isActive and date range and usage limits
        if (!coupon.IsValid())
            return (false, "This coupon is no longer valid.", null, 0);

        // 3. Check targetType
        if (coupon.TargetType == "User")
        {
            if (coupon.TargetUserId.HasValue && coupon.TargetUserId.Value != userId)
                return (false, "This coupon is not available for your account.", null, 0);
        }
        else if (coupon.TargetType == "Product")
        {
            // Check product type match
            if (!string.IsNullOrEmpty(coupon.TargetProductType) &&
                !coupon.TargetProductType.Equals(productType, StringComparison.OrdinalIgnoreCase))
                return (false, $"This coupon is only valid for {coupon.TargetProductType} purchases.", null, 0);

            // Check specific product match
            if (coupon.TargetProductId.HasValue && coupon.TargetProductId.Value != productId)
                return (false, "This coupon is not valid for this product.", null, 0);
        }

        // 4. Check mentorUserId scope
        if (coupon.MentorUserId.HasValue)
        {
            // If the coupon is mentor-scoped, the product must belong to that mentor
            if (mentorUserId.HasValue && coupon.MentorUserId.Value != mentorUserId.Value)
                return (false, "This coupon is only valid for a specific mentor's offerings.", null, 0);

            // If mentorUserId is not provided in request, try to resolve it
            if (!mentorUserId.HasValue)
                return (false, "Cannot verify mentor scope for this coupon.", null, 0);
        }

        // 5. Check per-user usage limit
        if (coupon.MaxUsagePerUser.HasValue)
        {
            var userUsageCount = await _db.CouponUsages
                .CountAsync(cu => cu.CouponId == coupon.Id && cu.UserId == userId);
            if (userUsageCount >= coupon.MaxUsagePerUser.Value)
                return (false, "You have reached the maximum usage limit for this coupon.", null, 0);
        }

        // 6. Calculate estimated discount
        decimal estimatedDiscount = 0;
        if (orderAmount > 0)
        {
            estimatedDiscount = coupon.CalculateDiscount(orderAmount);
            if (estimatedDiscount <= 0)
                return (false, "Order amount does not meet the minimum requirement for this coupon.", null, 0);
        }
        else
        {
            // For validation without order amount, return the discount value as-is
            estimatedDiscount = coupon.DiscountType == "Percentage"
                ? coupon.DiscountValue // percentage value
                : coupon.DiscountValue; // fixed amount
        }

        return (true, "Coupon is valid.", coupon, estimatedDiscount);
    }

    private async Task<Guid?> ResolveMentorForOrder(Order order)
    {
        switch (order.Type)
        {
            case OrderType.Booking:
                var booking = await _db.Bookings.AsNoTracking()
                    .Where(b => b.Id == order.ResourceId)
                    .Select(b => (Guid?)b.MentorUserId)
                    .FirstOrDefaultAsync();
                return booking;

            case OrderType.GroupClass:
                // ResourceId is enrollment ID, need to get class -> mentor
                var enrollment = await _db.ClassEnrollments.AsNoTracking()
                    .Where(e => e.Id == order.ResourceId)
                    .Select(e => e.ClassId)
                    .FirstOrDefaultAsync();
                if (enrollment != Guid.Empty)
                {
                    var mentorId = await _db.GroupClasses.AsNoTracking()
                        .Where(gc => gc.Id == enrollment)
                        .Select(gc => (Guid?)gc.MentorUserId)
                        .FirstOrDefaultAsync();
                    return mentorId;
                }
                return null;

            case OrderType.Course:
                var course = await _db.Courses.AsNoTracking()
                    .Where(c => c.Id == order.ResourceId)
                    .Select(c => (Guid?)c.MentorUserId)
                    .FirstOrDefaultAsync();
                return course;

            default:
                return null;
        }
    }

    private static string? ValidateCreateRequest(CreateCouponRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return "Coupon code is required.";

        if (request.Code.Length < 3 || request.Code.Length > 50)
            return "Coupon code must be between 3 and 50 characters.";

        var validDiscountTypes = new[] { "Percentage", "FixedAmount" };
        if (!validDiscountTypes.Contains(request.DiscountType))
            return "DiscountType must be 'Percentage' or 'FixedAmount'.";

        if (request.DiscountValue <= 0)
            return "DiscountValue must be greater than 0.";

        if (request.DiscountType == "Percentage" && request.DiscountValue > 100)
            return "Percentage discount cannot exceed 100.";

        var validTargetTypes = new[] { "All", "User", "Product" };
        if (!validTargetTypes.Contains(request.TargetType))
            return "TargetType must be 'All', 'User', or 'Product'.";

        if (request.TargetType == "User" && !request.TargetUserId.HasValue)
            return "TargetUserId is required when TargetType is 'User'.";

        if (request.EndDate.HasValue && request.StartDate.HasValue && request.EndDate < request.StartDate)
            return "EndDate must be after StartDate.";

        return null;
    }
}
