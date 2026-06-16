using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Api.Domain.Entities;

namespace Planora.Api.Infrastructure.Data.Configurations;

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");
        builder.HasKey(wm => new { wm.WorkspaceId, wm.UserId });

        builder.Property(wm => wm.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(wm => wm.User)
            .WithMany(u => u.WorkspaceMemberships)
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
