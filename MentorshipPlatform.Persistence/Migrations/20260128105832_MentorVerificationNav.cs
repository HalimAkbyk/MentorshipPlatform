using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MentorVerificationNav : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MentorProfileUserId",
                table: "MentorVerifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorVerifications_MentorProfileUserId",
                table: "MentorVerifications",
                column: "MentorProfileUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MentorVerifications_MentorProfiles_MentorProfileUserId",
                table: "MentorVerifications",
                column: "MentorProfileUserId",
                principalTable: "MentorProfiles",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MentorVerifications_MentorProfiles_MentorProfileUserId",
                table: "MentorVerifications");

            migrationBuilder.DropIndex(
                name: "IX_MentorVerifications_MentorProfileUserId",
                table: "MentorVerifications");

            migrationBuilder.DropColumn(
                name: "MentorProfileUserId",
                table: "MentorVerifications");
        }
    }
}
