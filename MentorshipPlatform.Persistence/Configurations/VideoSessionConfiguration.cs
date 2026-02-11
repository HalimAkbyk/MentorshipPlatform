using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class VideoSessionConfiguration : IEntityTypeConfiguration<VideoSession>
{
    public void Configure(EntityTypeBuilder<VideoSession> builder)
    {
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

    }
}