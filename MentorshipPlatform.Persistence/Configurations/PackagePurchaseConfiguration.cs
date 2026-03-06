using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class PackagePurchaseConfiguration : IEntityTypeConfiguration<PackagePurchase>
{
    public void Configure(EntityTypeBuilder<PackagePurchase> builder)
    {
        builder.ToTable("PackagePurchases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PurchasePrice).HasPrecision(18, 2);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Package)
            .WithMany()
            .HasForeignKey(e => e.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Order)
            .WithMany()
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.StudentId);
    }
}
