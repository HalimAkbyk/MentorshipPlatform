using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("PayoutRequests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MentorUserId).IsRequired();
        builder.HasIndex(e => e.MentorUserId);

        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.MentorNote).HasMaxLength(500);
        builder.Property(e => e.AdminNote).HasMaxLength(500);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
    }
}
