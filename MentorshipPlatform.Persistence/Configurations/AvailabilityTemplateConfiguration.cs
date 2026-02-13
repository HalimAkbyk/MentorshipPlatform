using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class AvailabilityTemplateConfiguration : IEntityTypeConfiguration<AvailabilityTemplate>
{
    public void Configure(EntityTypeBuilder<AvailabilityTemplate> builder)
    {
        builder.ToTable("AvailabilityTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Timezone).HasMaxLength(50).IsRequired();
        builder.Property(x => x.MinNoticeHours).HasDefaultValue(2);
        builder.Property(x => x.MaxBookingDaysAhead).HasDefaultValue(60);
        builder.Property(x => x.BufferAfterMin).HasDefaultValue(15);
        builder.Property(x => x.SlotGranularityMin).HasDefaultValue(30);
        builder.Property(x => x.MaxBookingsPerDay).HasDefaultValue(5);

        builder.HasIndex(x => x.MentorUserId)
            .HasDatabaseName("IX_AvailabilityTemplates_MentorUserId");

        builder.HasMany(x => x.Rules)
            .WithOne()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Overrides)
            .WithOne()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("AvailabilityRules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.SlotIndex).HasDefaultValue(0);

        builder.HasIndex(x => new { x.TemplateId, x.DayOfWeek, x.SlotIndex })
            .HasDatabaseName("IX_AvailabilityRules_Template_Day_Slot");
    }
}

public class AvailabilityOverrideConfiguration : IEntityTypeConfiguration<AvailabilityOverride>
{
    public void Configure(EntityTypeBuilder<AvailabilityOverride> builder)
    {
        builder.ToTable("AvailabilityOverrides");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(200);

        builder.HasIndex(x => new { x.TemplateId, x.Date })
            .HasDatabaseName("IX_AvailabilityOverrides_Template_Date");
    }
}
