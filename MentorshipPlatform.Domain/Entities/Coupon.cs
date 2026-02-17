using MentorshipPlatform.Domain.Common;

namespace MentorshipPlatform.Domain.Entities;

public class Coupon : BaseEntity
{
    public string Code { get; private set; } = string.Empty; // Unique, uppercase
    public string? Description { get; private set; }

    // Discount type
    public string DiscountType { get; private set; } = "Percentage"; // Percentage, FixedAmount
    public decimal DiscountValue { get; private set; } // e.g., 15 for 15% or 50 for 50 TRY
    public decimal? MaxDiscountAmount { get; private set; } // Cap for percentage discounts
    public decimal? MinOrderAmount { get; private set; } // Minimum order amount to use coupon

    // Scope
    public Guid CreatedByUserId { get; private set; } // Admin or Mentor who created it
    public string CreatedByRole { get; private set; } = "Admin"; // Admin, Mentor

    // Targeting
    public string TargetType { get; private set; } = "All"; // All, User, Product
    public Guid? TargetUserId { get; private set; } // Specific user (for personalized coupons)
    public string? TargetProductType { get; private set; } // Booking, Course, GroupClass (null = all)
    public Guid? TargetProductId { get; private set; } // Specific course/class (null = all of type)

    // For mentor-created coupons: only applies to their own offerings
    public Guid? MentorUserId { get; private set; } // If set, coupon only works for this mentor's products

    // Usage limits
    public int? MaxUsageCount { get; private set; } // Total usage limit (null = unlimited)
    public int? MaxUsagePerUser { get; private set; } // Per-user usage limit (null = unlimited)
    public int CurrentUsageCount { get; private set; } = 0;

    // Validity
    public bool IsActive { get; private set; } = true;
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }

    private Coupon() { }

    public static Coupon Create(
        string code,
        string? description,
        string discountType,
        decimal discountValue,
        decimal? maxDiscountAmount,
        decimal? minOrderAmount,
        Guid createdByUserId,
        string createdByRole,
        string targetType,
        Guid? targetUserId,
        string? targetProductType,
        Guid? targetProductId,
        Guid? mentorUserId,
        int? maxUsageCount,
        int? maxUsagePerUser,
        DateTime? startDate,
        DateTime? endDate)
    {
        return new Coupon
        {
            Code = code.ToUpper().Trim(),
            Description = description,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountAmount = maxDiscountAmount,
            MinOrderAmount = minOrderAmount,
            CreatedByUserId = createdByUserId,
            CreatedByRole = createdByRole,
            TargetType = targetType,
            TargetUserId = targetUserId,
            TargetProductType = targetProductType,
            TargetProductId = targetProductId,
            MentorUserId = mentorUserId,
            MaxUsageCount = maxUsageCount,
            MaxUsagePerUser = maxUsagePerUser,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    public void Update(string? description, decimal discountValue, decimal? maxDiscountAmount, decimal? minOrderAmount, int? maxUsageCount, int? maxUsagePerUser, DateTime? startDate, DateTime? endDate)
    {
        Description = description;
        DiscountValue = discountValue;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderAmount = minOrderAmount;
        MaxUsageCount = maxUsageCount;
        MaxUsagePerUser = maxUsagePerUser;
        StartDate = startDate;
        EndDate = endDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void IncrementUsage() { CurrentUsageCount++; UpdatedAt = DateTime.UtcNow; }

    public bool IsValid()
    {
        if (!IsActive) return false;
        var now = DateTime.UtcNow;
        if (StartDate.HasValue && now < StartDate.Value) return false;
        if (EndDate.HasValue && now > EndDate.Value) return false;
        if (MaxUsageCount.HasValue && CurrentUsageCount >= MaxUsageCount.Value) return false;
        return true;
    }

    public decimal CalculateDiscount(decimal orderAmount)
    {
        if (MinOrderAmount.HasValue && orderAmount < MinOrderAmount.Value) return 0;

        decimal discount = DiscountType == "Percentage"
            ? orderAmount * DiscountValue / 100
            : DiscountValue;

        if (MaxDiscountAmount.HasValue && discount > MaxDiscountAmount.Value)
            discount = MaxDiscountAmount.Value;

        if (discount > orderAmount)
            discount = orderAmount;

        return Math.Round(discount, 2);
    }
}
