using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MentorshipPlatform.Domain.Entities;

namespace MentorshipPlatform.Persistence.Configurations;

public class BulkNotificationConfiguration : IEntityTypeConfiguration<BulkNotification>
{
    public void Configure(EntityTypeBuilder<BulkNotification> builder)
    {
        builder.ToTable("BulkNotifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.TargetAudience).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Channel).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(50);
    }
}
