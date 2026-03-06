using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class InstructorSessionLogConfiguration : IEntityTypeConfiguration<InstructorSessionLog>
{
    public void Configure(EntityTypeBuilder<InstructorSessionLog> builder)
    {
        builder.ToTable("InstructorSessionLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SessionType).HasConversion<string>().HasMaxLength(30);
        builder.Ignore(e => e.DurationMinutes);

        builder.HasOne(e => e.Instructor)
            .WithMany()
            .HasForeignKey(e => e.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.VideoParticipant)
            .WithMany()
            .HasForeignKey(e => e.VideoParticipantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.InstructorId);
        builder.HasIndex(e => e.SessionId);
    }
}
