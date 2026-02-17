using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasMaxLength(5000)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TargetAudience)
            .HasMaxLength(30)
            .IsRequired();
    }
}
