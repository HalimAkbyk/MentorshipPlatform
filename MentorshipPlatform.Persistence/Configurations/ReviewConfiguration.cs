using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;


public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(b => b.Id);
        
        builder.HasOne(b => b.AuthorUser)
            .WithMany()
            .HasForeignKey(b => b.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.MentorUser)
            .WithMany()
            .HasForeignKey(b => b.MentorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.AuthorUserId);
        builder.HasIndex(b => b.MentorUserId);
    }
}