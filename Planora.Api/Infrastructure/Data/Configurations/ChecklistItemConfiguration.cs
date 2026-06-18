using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("checklist_items");
        builder.Property(i => i.Text).HasMaxLength(500).IsRequired();
        builder.HasOne(i => i.Checklist)
               .WithMany(c => c.Items)
               .HasForeignKey(i => i.ChecklistId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.ChecklistId);
    }
}
