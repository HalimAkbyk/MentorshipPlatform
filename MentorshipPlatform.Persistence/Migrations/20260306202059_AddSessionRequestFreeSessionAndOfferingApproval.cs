using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRequestFreeSessionAndOfferingApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdminApprovedPrice",
                table: "Offerings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminPriceNote",
                table: "Offerings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Offerings",
                type: "integer",
                nullable: false,
                defaultValue: 2); // 2 = Approved — existing offerings stay visible

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Offerings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FreeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreeSessions_CreditTransactions_CreditTransactionId",
                        column: x => x.CreditTransactionId,
                        principalTable: "CreditTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FreeSessions_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FreeSessions_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    StudentNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionRequests_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FreeSessions_CreditTransactionId",
                table: "FreeSessions",
                column: "CreditTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeSessions_MentorUserId",
                table: "FreeSessions",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FreeSessions_Status",
                table: "FreeSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FreeSessions_StudentUserId",
                table: "FreeSessions",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_BookingId",
                table: "SessionRequests",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_MentorUserId",
                table: "SessionRequests",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_OfferingId",
                table: "SessionRequests",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_Status",
                table: "SessionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRequests_StudentUserId",
                table: "SessionRequests",
                column: "StudentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreeSessions");

            migrationBuilder.DropTable(
                name: "SessionRequests");

            migrationBuilder.DropColumn(
                name: "AdminApprovedPrice",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "AdminPriceNote",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Offerings");
        }
    }
}
