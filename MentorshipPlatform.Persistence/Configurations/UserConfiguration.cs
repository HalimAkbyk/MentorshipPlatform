
using System.Text.Json;
using MentorshipPlatform.Domain.Entities;
using MentorshipPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(255);

        builder.Property(u => u.Phone)
            .HasMaxLength(20);

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Store roles as JSON array
        builder.Property(u => u.Roles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<UserRole>>(v, (JsonSerializerOptions)null!)!)
            .HasColumnType("jsonb");

        builder.Property(u => u.ExternalProvider)
            .HasMaxLength(20);

        builder.Property(u => u.ExternalId)
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Phone).IsUnique();

        builder.HasIndex(u => new { u.ExternalProvider, u.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalProvider\" IS NOT NULL");

        builder.HasOne(u => u.MentorProfile)
            .WithOne(m => m.User)
            .HasForeignKey<MentorProfile>(m => m.UserId);
    }
}