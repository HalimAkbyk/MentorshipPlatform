using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.ShortDescription).HasMaxLength(500);
        builder.Property(x => x.CoverImageUrl).HasMaxLength(500);
        builder.Property(x => x.PromoVideoKey).HasMaxLength(500);
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.Language).HasMaxLength(50).HasDefaultValue("tr");

        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("TRY");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Level)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.WhatYouWillLearnJson).HasColumnType("jsonb");
        builder.Property(x => x.RequirementsJson).HasColumnType("jsonb");
        builder.Property(x => x.TargetAudienceJson).HasColumnType("jsonb");

        builder.Property(x => x.RatingAvg).HasPrecision(3, 2);

        // Navigation: Course -> Sections
        builder.HasMany(x => x.Sections)
            .WithOne(s => s.Course)
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: Course -> MentorUser
        builder.HasOne(x => x.MentorUser)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.MentorUserId, x.Status });
    }
}
