using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseReviewSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseReviewRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MentorNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdminGeneralNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseReviewRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseReviewRounds_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LectureReviewComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureId = table.Column<Guid>(type: "uuid", nullable: true),
                    LectureTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VideoKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Flag = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureReviewComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LectureReviewComments_CourseLectures_LectureId",
                        column: x => x.LectureId,
                        principalTable: "CourseLectures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LectureReviewComments_CourseReviewRounds_ReviewRoundId",
                        column: x => x.ReviewRoundId,
                        principalTable: "CourseReviewRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviewRounds_CourseId",
                table: "CourseReviewRounds",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviewRounds_CourseId_RoundNumber",
                table: "CourseReviewRounds",
                columns: new[] { "CourseId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_LectureReviewComments_LectureId",
                table: "LectureReviewComments",
                column: "LectureId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureReviewComments_ReviewRoundId",
                table: "LectureReviewComments",
                column: "ReviewRoundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LectureReviewComments");

            migrationBuilder.DropTable(
                name: "CourseReviewRounds");
        }
    }
}
