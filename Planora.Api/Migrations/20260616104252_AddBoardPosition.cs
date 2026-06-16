using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_boards_WorkspaceId",
                table: "boards");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "boards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_boards_WorkspaceId_Position",
                table: "boards",
                columns: new[] { "WorkspaceId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_boards_WorkspaceId_Position",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "boards");

            migrationBuilder.CreateIndex(
                name: "IX_boards_WorkspaceId",
                table: "boards",
                column: "WorkspaceId");
        }
    }
}
