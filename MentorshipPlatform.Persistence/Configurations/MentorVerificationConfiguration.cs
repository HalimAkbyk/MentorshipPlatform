using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;


public class MentorVerificationConfiguration : IEntityTypeConfiguration<MentorVerification>
{
    public void Configure(EntityTypeBuilder<MentorVerification> builder)
    {
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
            
        // ✅ Relationship MentorProfileConfiguration'da tanımlandı
        // Burada tekrar tanımlamaya gerek yok
    }
}