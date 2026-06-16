using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(5000);

        builder.Property(c => c.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.Color)
            .HasMaxLength(9);

        builder.HasOne(c => c.Assignee)
            .WithMany()
            .HasForeignKey(c => c.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.ColumnId, c.Position });

        // Soft-delete filter: archived cards are hidden by default
        builder.HasQueryFilter(c => !c.IsArchived);
    }
}
