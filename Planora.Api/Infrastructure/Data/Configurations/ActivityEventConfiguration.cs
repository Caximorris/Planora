using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("activity_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Verb)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(e => e.TargetType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.HasOne(e => e.Actor)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Workspace)
            .WithMany()
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Board)
            .WithMany()
            .HasForeignKey(e => e.BoardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => new { e.WorkspaceId, e.CreatedAt });
        builder.HasIndex(e => new { e.BoardId, e.CreatedAt });
        builder.HasIndex(e => new { e.TargetType, e.TargetId });
    }
}
