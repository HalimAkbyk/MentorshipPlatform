using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class HomepageModuleConfiguration : IEntityTypeConfiguration<HomepageModule>
{
    public void Configure(EntityTypeBuilder<HomepageModule> builder)
    {
        builder.Property(x => x.ModuleType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Subtitle)
            .HasMaxLength(500);
    }
}
