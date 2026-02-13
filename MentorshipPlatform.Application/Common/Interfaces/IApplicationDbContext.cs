using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MentorshipPlatform.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<MentorProfile> MentorProfiles { get; }
    DbSet<MentorVerification> MentorVerifications { get; }
    DbSet<Offering> Offerings { get; }
    DbSet<AvailabilitySlot> AvailabilitySlots { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<GroupClass> GroupClasses { get; }
    DbSet<ClassEnrollment> ClassEnrollments { get; }
    DbSet<Order> Orders { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<VideoSession> VideoSessions { get; }
    DbSet<VideoParticipant> VideoParticipants { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ProcessHistory> ProcessHistories { get; }
    DbSet<AvailabilityTemplate> AvailabilityTemplates { get; }
    DbSet<AvailabilityRule> AvailabilityRules { get; }
    DbSet<AvailabilityOverride> AvailabilityOverrides { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}