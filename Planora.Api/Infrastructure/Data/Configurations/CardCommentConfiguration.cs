using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class CardCommentConfiguration : IEntityTypeConfiguration<CardComment>
{
    public void Configure(EntityTypeBuilder<CardComment> builder)
    {
        builder.ToTable("card_comments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text).IsRequired().HasMaxLength(4000);
        builder.Property(c => c.AuthorId).HasMaxLength(450);

        builder.HasIndex(c => c.CardId);

        builder.HasOne(c => c.Card)
            .WithMany(card => card.Comments)
            .HasForeignKey(c => c.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
