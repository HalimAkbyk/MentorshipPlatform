using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class MentorProfileConfiguration : IEntityTypeConfiguration<MentorProfile>
{
    public void Configure(EntityTypeBuilder<MentorProfile> builder)
    {
        builder.HasKey(m => m.UserId);
        
        builder.Property(m => m.Bio)
            .HasMaxLength(2000);

        builder.Property(m => m.University)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Department)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Headline)
            .HasMaxLength(300);

        builder.Property(m => m.RatingAvg)
            .HasPrecision(3, 2);

        // ✅ Verifications relationship - backing field kullan
        builder.HasMany(m => m.Verifications)
            .WithOne(v => v.MentorProfile)
            .HasForeignKey(v => v.MentorUserId)
            .HasPrincipalKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // ✅ Backing field'i metadata'ya ekle
        builder.Metadata
            .FindNavigation(nameof(MentorProfile.Verifications))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Offerings)
            .WithOne()
            .HasForeignKey(o => o.MentorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.IsListed);
        builder.HasIndex(m => m.University);
    }
}