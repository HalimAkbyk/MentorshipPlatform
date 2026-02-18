using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseContentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CourseLectures",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "CourseAdminNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoteType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Flag = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LectureTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAdminNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAdminNotes_CourseLectures_LectureId",
                        column: x => x.LectureId,
                        principalTable: "CourseLectures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseAdminNotes_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseAdminNotes_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdminNotes_AdminUserId",
                table: "CourseAdminNotes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdminNotes_CourseId",
                table: "CourseAdminNotes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAdminNotes_LectureId",
                table: "CourseAdminNotes",
                column: "LectureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseAdminNotes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CourseLectures");
        }
    }
}
