using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("boards");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Description)
            .HasMaxLength(500);

        builder.Property(b => b.CoverColor)
            .HasMaxLength(7);

        builder.Property(b => b.CoverImageUrl)
            .HasMaxLength(500);

        builder.HasMany(b => b.Columns)
            .WithOne(c => c.Board)
            .HasForeignKey(c => c.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.WorkspaceId, b.Position });
        // Supports the workspace trash listing (boards where DeletedAt != null)
        builder.HasIndex(b => new { b.WorkspaceId, b.DeletedAt });

        // Global filter hides both archived (put-aside) and trashed (soft-deleted) boards by
        // default. Both fold into one predicate — EF Core allows a single query filter per entity.
        builder.HasQueryFilter(b => !b.IsArchived && b.DeletedAt == null);
    }
}
