using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class WorkspaceLabelConfiguration : IEntityTypeConfiguration<WorkspaceLabel>
{
    public void Configure(EntityTypeBuilder<WorkspaceLabel> builder)
    {
        builder.ToTable("workspace_labels");
        builder.Property(l => l.Name).HasMaxLength(50).IsRequired();
        builder.Property(l => l.Color).HasMaxLength(9).IsRequired();
        builder.HasOne(l => l.Workspace)
               .WithMany(w => w.Labels)
               .HasForeignKey(l => l.WorkspaceId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(l => l.WorkspaceId);
    }
}
