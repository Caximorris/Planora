using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToBoardsAndCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "boards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_ColumnId_DeletedAt",
                table: "cards",
                columns: new[] { "ColumnId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_boards_WorkspaceId_DeletedAt",
                table: "boards",
                columns: new[] { "WorkspaceId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cards_ColumnId_DeletedAt",
                table: "cards");

            migrationBuilder.DropIndex(
                name: "IX_boards_WorkspaceId_DeletedAt",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "boards");
        }
    }
}
