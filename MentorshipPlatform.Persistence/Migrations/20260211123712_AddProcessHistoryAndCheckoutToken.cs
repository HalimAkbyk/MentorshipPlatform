using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessHistoryAndCheckoutToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "CheckoutToken",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PerformedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PerformedByRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessHistories_CreatedAt",
                table: "ProcessHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessHistories_EntityType_EntityId",
                table: "ProcessHistories",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessHistories_PerformedBy",
                table: "ProcessHistories",
                column: "PerformedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessHistories");

            migrationBuilder.DropColumn(
                name: "CheckoutToken",
                table: "Orders");

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
    }
}
