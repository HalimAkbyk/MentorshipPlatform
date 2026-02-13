using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("AvailabilitySlots");

        builder.HasKey(x => x.Id);

        // Composite index (MentorUserId, StartAt)
        builder.HasIndex(x => new { x.MentorUserId, x.StartAt })
            .HasDatabaseName("IX_AvailabilitySlots_MentorUserId_StartAt");

        // TemplateId - hangi template'den oluşturulduğu
        builder.Property(x => x.TemplateId).IsRequired(false);
        builder.HasIndex(x => x.TemplateId)
            .HasDatabaseName("IX_AvailabilitySlots_TemplateId");
    }
}