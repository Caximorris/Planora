using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_InviterUserId",
                table: "workspace_invitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_Token",
                table: "workspace_invitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_WorkspaceId_InviteeEmail",
                table: "workspace_invitations",
                columns: new[] { "WorkspaceId", "InviteeEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_invitations");
        }
    }
}
