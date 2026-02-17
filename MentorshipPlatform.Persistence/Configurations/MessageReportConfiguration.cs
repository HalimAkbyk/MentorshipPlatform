using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class MessageReportConfiguration : IEntityTypeConfiguration<MessageReport>
{
    public void Configure(EntityTypeBuilder<MessageReport> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.AdminNotes)
            .HasMaxLength(1000);

        builder.HasOne(r => r.Message)
            .WithMany()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReporterUser)
            .WithMany()
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.MessageId);
        builder.HasIndex(r => r.ReporterUserId);
        builder.HasIndex(r => r.Status);
    }
}
