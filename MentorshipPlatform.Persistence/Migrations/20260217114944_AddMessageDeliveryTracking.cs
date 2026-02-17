using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageNotificationLogs_BookingId_RecipientUserId_SentAt",
                table: "MessageNotificationLogs",
                columns: new[] { "BookingId", "RecipientUserId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageNotificationLogs");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Messages");
        }
    }
}
