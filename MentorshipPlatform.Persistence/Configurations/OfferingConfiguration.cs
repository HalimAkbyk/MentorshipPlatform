using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class OfferingConfiguration :IEntityTypeConfiguration<Offering>
{
    public void Configure(EntityTypeBuilder<Offering> builder)
    {
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}