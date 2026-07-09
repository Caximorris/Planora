using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardCoverImageFit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageFit",
                table: "boards",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageFit",
                table: "boards");
        }
    }
}
