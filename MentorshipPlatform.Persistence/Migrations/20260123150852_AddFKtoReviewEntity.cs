using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFKtoReviewEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reviews_AuthorUserId",
                table: "Reviews",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MentorUserId",
                table: "Reviews",
                column: "MentorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_AuthorUserId",
                table: "Reviews",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_MentorUserId",
                table: "Reviews",
                column: "MentorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_AuthorUserId",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_MentorUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_AuthorUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_MentorUserId",
                table: "Reviews");
        }
    }
}
