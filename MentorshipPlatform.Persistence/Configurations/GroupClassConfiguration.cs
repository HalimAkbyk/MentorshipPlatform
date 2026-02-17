using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;


public class GroupClassConfiguration : IEntityTypeConfiguration<GroupClass>
{
    public void Configure(EntityTypeBuilder<GroupClass> builder)
    {
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CoverImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Currency)
            .HasMaxLength(10);

        builder.HasMany(x => x.Enrollments)
            .WithOne(x => x.Class)
            .HasForeignKey(x => x.ClassId);
    }
}