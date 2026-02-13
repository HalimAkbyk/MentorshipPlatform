using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Offerings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Offerings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Offerings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "TRY",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Offerings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Offerings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetailedDescription",
                table: "Offerings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxBookingDaysAhead",
                table: "Offerings",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "MinNoticeHours",
                table: "Offerings",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "Offerings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "Offerings",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingQuestions_Offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "Offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingQuestionResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingQuestionResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingQuestionResponses_BookingQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "BookingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Offerings_IsActive",
                table: "Offerings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Offerings_MentorUserId_SortOrder",
                table: "Offerings",
                columns: new[] { "MentorUserId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingQuestionResponses_BookingId",
                table: "BookingQuestionResponses",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingQuestionResponses_BookingId_QuestionId",
                table: "BookingQuestionResponses",
                columns: new[] { "BookingId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingQuestionResponses_QuestionId",
                table: "BookingQuestionResponses",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingQuestions_OfferingId",
                table: "BookingQuestions",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingQuestions_OfferingId_SortOrder",
                table: "BookingQuestions",
                columns: new[] { "OfferingId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingQuestionResponses");

            migrationBuilder.DropTable(
                name: "BookingQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Offerings_IsActive",
                table: "Offerings");

            migrationBuilder.DropIndex(
                name: "IX_Offerings_MentorUserId_SortOrder",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "DetailedDescription",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "MaxBookingDaysAhead",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "MinNoticeHours",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "SessionType",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "Offerings");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Offerings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Offerings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Offerings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldDefaultValue: "TRY");
        }
    }
}
