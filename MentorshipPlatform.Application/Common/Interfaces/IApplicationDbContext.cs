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
    DbSet<BookingQuestion> BookingQuestions { get; }
    DbSet<BookingQuestionResponse> BookingQuestionResponses { get; }
    DbSet<PresetAvatar> PresetAvatars { get; }

    // Course entities
    DbSet<Course> Courses { get; }
    DbSet<CourseSection> CourseSections { get; }
    DbSet<CourseLecture> CourseLectures { get; }
    DbSet<CourseEnrollment> CourseEnrollments { get; }
    DbSet<LectureProgress> LectureProgresses { get; }
    DbSet<LectureNote> LectureNotes { get; }

    // Refund entities
    DbSet<RefundRequest> RefundRequests { get; }

    // Messaging entities
    DbSet<Message> Messages { get; }
    DbSet<MessageReport> MessageReports { get; }
    DbSet<MessageNotificationLog> MessageNotificationLogs { get; }

    // CMS entities
    DbSet<HomepageModule> HomepageModules { get; }
    DbSet<Banner> Banners { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<StaticPage> StaticPages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}