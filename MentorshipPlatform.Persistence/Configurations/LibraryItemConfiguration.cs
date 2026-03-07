using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class LibraryItemConfiguration : IEntityTypeConfiguration<LibraryItem>
{
    public void Configure(EntityTypeBuilder<LibraryItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.ItemType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.FileFormat)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.FileUrl).HasMaxLength(500);
        builder.Property(x => x.OriginalFileName).HasMaxLength(300);
        builder.Property(x => x.ExternalUrl).HasMaxLength(500);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Subject).HasMaxLength(100);
        builder.Property(x => x.TemplateType).HasMaxLength(100);

        builder.Property(x => x.TagsJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.ItemType);
    }
}
