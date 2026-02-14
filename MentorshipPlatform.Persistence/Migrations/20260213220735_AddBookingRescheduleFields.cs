using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRescheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingRescheduleEndAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PendingRescheduleRequestedBy",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingRescheduleStartAt",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCountMentor",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleCountStudent",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingRescheduleEndAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PendingRescheduleRequestedBy",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PendingRescheduleStartAt",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RescheduleCountMentor",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RescheduleCountStudent",
                table: "Bookings");
        }
    }
}
