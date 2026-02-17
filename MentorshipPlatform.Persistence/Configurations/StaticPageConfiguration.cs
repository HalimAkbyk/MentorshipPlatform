using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class StaticPageConfiguration : IEntityTypeConfiguration<StaticPage>
{
    public void Configure(EntityTypeBuilder<StaticPage> builder)
    {
        builder.HasIndex(x => x.Slug)
            .IsUnique();

        builder.Property(x => x.Slug)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.MetaTitle)
            .HasMaxLength(200);

        builder.Property(x => x.MetaDescription)
            .HasMaxLength(500);
    }
}
