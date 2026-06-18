using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InviterUserId).HasMaxLength(450);
        builder.Property(i => i.InviteeEmail).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Token).HasMaxLength(100).IsRequired();

        builder.HasIndex(i => i.Token).IsUnique();
        builder.HasIndex(i => new { i.WorkspaceId, i.InviteeEmail });

        builder.HasOne(i => i.Workspace)
            .WithMany(w => w.Invitations)
            .HasForeignKey(i => i.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Inviter)
            .WithMany()
            .HasForeignKey(i => i.InviterUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
