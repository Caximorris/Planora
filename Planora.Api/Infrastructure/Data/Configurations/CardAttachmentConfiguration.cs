using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class CardAttachmentConfiguration : IEntityTypeConfiguration<CardAttachment>
{
    public void Configure(EntityTypeBuilder<CardAttachment> builder)
    {
        builder.ToTable("card_attachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UploadedById).HasMaxLength(450);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Url).IsRequired().HasMaxLength(500);

        builder.HasIndex(a => a.CardId);

        builder.HasOne(a => a.Card)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Match Card's global filter so attachment reads never expose hidden cards by accident.
        builder.HasQueryFilter(a => !a.Card.IsArchived && a.Card.DeletedAt == null);
    }
}
