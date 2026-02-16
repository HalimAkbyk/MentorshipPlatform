using MediatR;
using MentorshipPlatform.Application.Common.Interfaces;
using MentorshipPlatform.Domain.Common;
using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IMediator mediator,
        ICurrentUserService currentUser) : base(options)
    {
        _mediator = mediator;
        _currentUser = currentUser;
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

    // Refund entities
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();

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

        domainEntities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}