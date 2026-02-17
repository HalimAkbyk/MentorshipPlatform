using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;

public class MessageNotificationLogConfiguration : IEntityTypeConfiguration<MessageNotificationLog>
{
    public void Configure(EntityTypeBuilder<MessageNotificationLog> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.BookingId, m.RecipientUserId, m.SentAt });
    }
}
