using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MentorshipPlatform.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApplicationDbContext> _logger;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IMediator mediator,
        ICurrentUserService currentUser,
        ILogger<ApplicationDbContext> logger) : base(options)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _logger = logger;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorVerification> MentorVerifications => Set<MentorVerification>();
    public DbSet<Offering> Offerings => Set<Offering>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<GroupClass> GroupClasses => Set<GroupClass>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<VideoSession> VideoSessions => Set<VideoSession>();
    public DbSet<VideoParticipant> VideoParticipants => Set<VideoParticipant>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProcessHistory> ProcessHistories => Set<ProcessHistory>();
    public DbSet<AvailabilityTemplate> AvailabilityTemplates => Set<AvailabilityTemplate>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<AvailabilityOverride> AvailabilityOverrides => Set<AvailabilityOverride>();
    public DbSet<BookingQuestion> BookingQuestions => Set<BookingQuestion>();
    public DbSet<BookingQuestionResponse> BookingQuestionResponses => Set<BookingQuestionResponse>();
    public DbSet<PresetAvatar> PresetAvatars => Set<PresetAvatar>();

    // Course entities
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseSection> CourseSections => Set<CourseSection>();
    public DbSet<CourseLecture> CourseLectures => Set<CourseLecture>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<LectureProgress> LectureProgresses => Set<LectureProgress>();
    public DbSet<LectureNote> LectureNotes => Set<LectureNote>();
    public DbSet<CourseReviewRound> CourseReviewRounds => Set<CourseReviewRound>();
    public DbSet<LectureReviewComment> LectureReviewComments => Set<LectureReviewComment>();
    public DbSet<CourseAdminNote> CourseAdminNotes => Set<CourseAdminNote>();

    // Refund entities
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();

    // Messaging entities
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageReport> MessageReports => Set<MessageReport>();
    public DbSet<MessageNotificationLog> MessageNotificationLogs => Set<MessageNotificationLog>();

    // CMS entities
    public DbSet<HomepageModule> HomepageModules => Set<HomepageModule>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<StaticPage> StaticPages => Set<StaticPage>();

    // Notification entities
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<BulkNotification> BulkNotifications => Set<BulkNotification>();

    // Moderation entities
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();

    // Platform settings entities
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    // Exam entities
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamAnswer> ExamAnswers => Set<ExamAnswer>();

    // Coupon entities
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    // Category entities
    public DbSet<Category> Categories => Set<Category>();

    // Admin notification entities
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();

    // User notification entities
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    // Payout entities
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();

    // Onboarding entities
    public DbSet<StudentOnboardingProfile> StudentOnboardingProfiles => Set<StudentOnboardingProfile>();
    public DbSet<MentorOnboardingProfile> MentorOnboardingProfiles => Set<MentorOnboardingProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events
        await DispatchDomainEventsAsync(cancellationToken);

        return result;
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var domainEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        if (domainEvents.Count > 0)
        {
            _logger.LogInformation("📧 Dispatching {Count} domain event(s): {EventTypes}",
                domainEvents.Count,
                string.Join(", ", domainEvents.Select(e => e.GetType().Name)));
        }

        domainEntities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📧 ❌ Error dispatching domain event {EventType}", domainEvent.GetType().Name);
                // Don't re-throw — don't break SaveChanges flow for event dispatch failures
            }
        }
    }
}