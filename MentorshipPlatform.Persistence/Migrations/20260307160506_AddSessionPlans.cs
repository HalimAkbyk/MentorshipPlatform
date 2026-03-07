using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    AssignmentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DifficultyLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxScore = table.Column<int>(type: "integer", nullable: true),
                    AllowLateSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    LatePenaltyPercent = table.Column<int>(type: "integer", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurriculumTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Curriculums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalWeeks = table.Column<int>(type: "integer", nullable: false),
                    EstimatedHoursPerWeek = table.Column<int>(type: "integer", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Curriculums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Curriculums_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurriculumTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreSessionNote = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SessionObjective = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    SessionNotes = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    AgendaItemsJson = table.Column<string>(type: "jsonb", nullable: true),
                    PostSessionSummary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    LinkedAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPlans_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionPlans_GroupClasses_GroupClassId",
                        column: x => x.GroupClassId,
                        principalTable: "GroupClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionPlans_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentMaterials_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentMaterials_LibraryItems_LibraryItemId",
                        column: x => x.LibraryItemId,
                        principalTable: "LibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    FileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentSubmissions_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentSubmissions_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumWeeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumWeeks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumWeeks_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentCurriculumEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCurriculumEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCurriculumEnrollments_Curriculums_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCurriculumEnrollments_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCurriculumEnrollments_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionPlanMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPlanMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPlanMaterials_LibraryItems_LibraryItemId",
                        column: x => x.LibraryItemId,
                        principalTable: "LibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionPlanMaterials_SessionPlans_SessionPlanId",
                        column: x => x.SessionPlanId,
                        principalTable: "SessionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionReviews_AssignmentSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "AssignmentSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionReviews_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: true),
                    ObjectiveText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkedExamId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumTopics_CurriculumWeeks_CurriculumWeekId",
                        column: x => x.CurriculumWeekId,
                        principalTable: "CurriculumWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumTopicMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MaterialRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumTopicMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumTopicMaterials_CurriculumTopics_CurriculumTopicId",
                        column: x => x.CurriculumTopicId,
                        principalTable: "CurriculumTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurriculumTopicMaterials_LibraryItems_LibraryItemId",
                        column: x => x.LibraryItemId,
                        principalTable: "LibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentCurriculumEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumTopicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MentorNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicProgresses_CurriculumTopics_CurriculumTopicId",
                        column: x => x.CurriculumTopicId,
                        principalTable: "CurriculumTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TopicProgresses_StudentCurriculumEnrollments_StudentCurricu~",
                        column: x => x.StudentCurriculumEnrollmentId,
                        principalTable: "StudentCurriculumEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentMaterials_AssignmentId",
                table: "AssignmentMaterials",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentMaterials_LibraryItemId",
                table: "AssignmentMaterials",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_MentorUserId",
                table: "Assignments",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_AssignmentId",
                table: "AssignmentSubmissions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_StudentUserId",
                table: "AssignmentSubmissions",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculums_MentorUserId",
                table: "Curriculums",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculums_Status",
                table: "Curriculums",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTopicMaterials_CurriculumTopicId",
                table: "CurriculumTopicMaterials",
                column: "CurriculumTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTopicMaterials_LibraryItemId",
                table: "CurriculumTopicMaterials",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTopics_CurriculumWeekId",
                table: "CurriculumTopics",
                column: "CurriculumWeekId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumWeeks_CurriculumId",
                table: "CurriculumWeeks",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlanMaterials_LibraryItemId",
                table: "SessionPlanMaterials",
                column: "LibraryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlanMaterials_SessionPlanId",
                table: "SessionPlanMaterials",
                column: "SessionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_BookingId",
                table: "SessionPlans",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_GroupClassId",
                table: "SessionPlans",
                column: "GroupClassId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_MentorUserId",
                table: "SessionPlans",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCurriculumEnrollments_CurriculumId",
                table: "StudentCurriculumEnrollments",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCurriculumEnrollments_MentorUserId",
                table: "StudentCurriculumEnrollments",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCurriculumEnrollments_StudentUserId",
                table: "StudentCurriculumEnrollments",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_MentorUserId",
                table: "SubmissionReviews",
                column: "MentorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionReviews_SubmissionId",
                table: "SubmissionReviews",
                column: "SubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicProgresses_CurriculumTopicId",
                table: "TopicProgresses",
                column: "CurriculumTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicProgresses_StudentCurriculumEnrollmentId",
                table: "TopicProgresses",
                column: "StudentCurriculumEnrollmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentMaterials");

            migrationBuilder.DropTable(
                name: "CurriculumTopicMaterials");

            migrationBuilder.DropTable(
                name: "SessionPlanMaterials");

            migrationBuilder.DropTable(
                name: "SubmissionReviews");

            migrationBuilder.DropTable(
                name: "TopicProgresses");

            migrationBuilder.DropTable(
                name: "SessionPlans");

            migrationBuilder.DropTable(
                name: "AssignmentSubmissions");

            migrationBuilder.DropTable(
                name: "CurriculumTopics");

            migrationBuilder.DropTable(
                name: "StudentCurriculumEnrollments");

            migrationBuilder.DropTable(
                name: "Assignments");

            migrationBuilder.DropTable(
                name: "CurriculumWeeks");

            migrationBuilder.DropTable(
                name: "Curriculums");
        }
    }
}
