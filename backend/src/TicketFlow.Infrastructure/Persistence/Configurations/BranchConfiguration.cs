using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.Property(b => b.Name).HasMaxLength(100).IsRequired();
        builder.Property(b => b.City).HasMaxLength(100).IsRequired();
        builder.Property(b => b.IsActive).HasDefaultValue(true);

        builder.HasIndex(b => b.Name).IsUnique();
    }
}
