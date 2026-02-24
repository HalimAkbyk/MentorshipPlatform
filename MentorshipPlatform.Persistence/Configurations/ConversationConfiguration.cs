using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(c => c.User1)
            .WithMany()
            .HasForeignKey(c => c.User1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.User2)
            .WithMany()
            .HasForeignKey(c => c.User2Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Booking)
            .WithMany()
            .HasForeignKey(c => c.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index for direct conversations (one per user pair)
        builder.HasIndex(c => new { c.User1Id, c.User2Id, c.Type })
            .HasFilter("\"Type\" = 'Direct'")
            .IsUnique();

        // Index for booking-based conversations
        builder.HasIndex(c => c.BookingId);

        // Index for user lookups
        builder.HasIndex(c => c.User1Id);
        builder.HasIndex(c => c.User2Id);
    }
}
