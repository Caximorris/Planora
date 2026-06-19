using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planora.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationBoardLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedBoardId",
                table: "notifications",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedBoardId",
                table: "notifications");
        }
    }
}
