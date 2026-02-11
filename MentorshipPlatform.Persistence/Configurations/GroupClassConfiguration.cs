using MentorshipPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MentorshipPlatform.Persistence.Configurations;


public class GroupClassConfiguration : IEntityTypeConfiguration<GroupClass>
{
    public void Configure(EntityTypeBuilder<GroupClass> builder)
    {
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

    }
}