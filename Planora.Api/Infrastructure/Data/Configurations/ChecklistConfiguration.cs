using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class ChecklistConfiguration : IEntityTypeConfiguration<Checklist>
{
    public void Configure(EntityTypeBuilder<Checklist> builder)
    {
        builder.ToTable("checklists");
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.HasOne(c => c.Card)
               .WithMany(card => card.Checklists)
               .HasForeignKey(c => c.CardId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.CardId);
    }
}
