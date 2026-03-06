
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

        // Store roles as JSON array — use backing field for change tracking
        builder.Property(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<UserRole>>(v, (JsonSerializerOptions)null!) ?? new List<UserRole>())
            .HasColumnType("jsonb")
            .Metadata.SetValueComparer(
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<UserRole>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));

        builder.Property(u => u.ExternalProvider)
            .HasMaxLength(20);

        builder.Property(u => u.ExternalId)
            .HasMaxLength(255);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Phone).IsUnique();

        builder.HasIndex(u => new { u.ExternalProvider, u.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalProvider\" IS NOT NULL");

        // Pivot: Instructor fields
        builder.Property(u => u.IsOwner).HasDefaultValue(false);
        builder.Property(u => u.InstructorStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(u => u.MentorProfile)
            .WithOne(m => m.User)
            .HasForeignKey<MentorProfile>(m => m.UserId);
    }
}