using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAvatarFlagged : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAvatarFlagged",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAvatarFlagged",
                table: "Users");
        }
    }
}
