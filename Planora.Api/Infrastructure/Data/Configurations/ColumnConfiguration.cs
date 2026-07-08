using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class ColumnConfiguration : IEntityTypeConfiguration<Column>
{
    public void Configure(EntityTypeBuilder<Column> builder)
    {
        // Explicit table name avoids any SQL reserved-word conflicts
        builder.ToTable("board_columns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Color)
            .HasMaxLength(9);

        builder.HasMany(c => c.Cards)
            .WithOne(card => card.Column)
            .HasForeignKey(card => card.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.BoardId, c.Position });

        // Mirrors Board's global query filter (archived + trashed) so EF never returns
        // columns belonging to a hidden board.
        builder.HasQueryFilter(c => !c.Board.IsArchived && c.Board.DeletedAt == null);
    }
}
