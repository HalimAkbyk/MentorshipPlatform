using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CourseLectureConfiguration : IEntityTypeConfiguration<CourseLecture>
{
    public void Configure(EntityTypeBuilder<CourseLecture> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.VideoKey).HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);
        builder.Property(x => x.TextContent).HasColumnType("text");

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => new { x.SectionId, x.SortOrder });
    }
}
