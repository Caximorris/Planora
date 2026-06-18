using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class CardLabelConfiguration : IEntityTypeConfiguration<CardLabel>
{
    public void Configure(EntityTypeBuilder<CardLabel> builder)
    {
        builder.ToTable("card_labels");
        builder.HasKey(cl => new { cl.CardId, cl.LabelId });
        builder.HasOne(cl => cl.Card)
               .WithMany(c => c.Labels)
               .HasForeignKey(cl => cl.CardId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(cl => cl.Label)
               .WithMany(l => l.CardLabels)
               .HasForeignKey(cl => cl.LabelId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
