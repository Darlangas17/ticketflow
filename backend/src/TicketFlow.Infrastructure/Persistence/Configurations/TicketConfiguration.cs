using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketFlow.Domain.Entities;

namespace TicketFlow.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.Property(t => t.Title).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Description).IsRequired(); // nvarchar(max)
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Catalogs and users cannot be deleted while tickets reference them
        // (they are deactivated instead). Restrict also avoids SQL Server's
        // "multiple cascade paths" error, since Tickets has two FKs to Users.
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Branch)
            .WithMany(b => b.Tickets)
            .HasForeignKey(t => t.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Requester)
            .WithMany(u => u.RequestedTickets)
            .HasForeignKey(t => t.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Assignee)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssigneeId);
        builder.HasIndex(t => t.BranchId);
        builder.HasIndex(t => t.CreatedAt);
    }
}
