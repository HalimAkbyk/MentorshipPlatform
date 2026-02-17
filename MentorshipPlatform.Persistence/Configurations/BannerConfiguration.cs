using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.ImageUrl)
            .HasMaxLength(500);

        builder.Property(x => x.LinkUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Position)
            .HasMaxLength(20)
            .IsRequired();
    }
}
