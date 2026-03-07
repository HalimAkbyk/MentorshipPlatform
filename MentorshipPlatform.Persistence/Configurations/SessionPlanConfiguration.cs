using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class SessionPlanConfiguration : IEntityTypeConfiguration<SessionPlan>
{
    public void Configure(EntityTypeBuilder<SessionPlan> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200);

        builder.Property(x => x.PreSessionNote)
            .HasMaxLength(5000);

        builder.Property(x => x.SessionObjective)
            .HasMaxLength(5000);

        builder.Property(x => x.PostSessionSummary)
            .HasMaxLength(5000);

        builder.Property(x => x.SessionNotes)
            .HasMaxLength(10000);

        builder.Property(x => x.AgendaItemsJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.IsTemplate)
            .HasDefaultValue(false);

        builder.Property(x => x.TemplateName)
            .HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(x => x.Mentor)
            .WithMany()
            .HasForeignKey(x => x.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Booking)
            .WithMany()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.GroupClass)
            .WithMany()
            .HasForeignKey(x => x.GroupClassId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MentorUserId);
        builder.HasIndex(x => x.BookingId);
        builder.HasIndex(x => x.GroupClassId);
        builder.HasIndex(x => x.IsTemplate);
    }
}

public class SessionPlanMaterialConfiguration : IEntityTypeConfiguration<SessionPlanMaterial>
{
    public void Configure(EntityTypeBuilder<SessionPlanMaterial> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Phase)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasOne(x => x.SessionPlan)
            .WithMany(x => x.Materials)
            .HasForeignKey(x => x.SessionPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.LibraryItem)
            .WithMany()
            .HasForeignKey(x => x.LibraryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SessionPlanId);
    }
}
