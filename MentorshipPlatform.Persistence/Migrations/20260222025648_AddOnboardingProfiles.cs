using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorOnboardingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Languages = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subtopics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TargetAudience = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExperienceLevels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    YearsOfExperience = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentRole = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CurrentCompany = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PreviousCompanies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Education = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Certifications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkedinUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GithubUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PortfolioUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    YksExamType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    YksScore = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    YksRanking = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MentoringTypes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SessionFormats = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OfferFreeIntro = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorOnboardingProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentOnboardingProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StatusDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Goals = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Categories = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subtopics = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Preferences = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BudgetMin = table.Column<int>(type: "integer", nullable: true),
                    BudgetMax = table.Column<int>(type: "integer", nullable: true),
                    Availability = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SessionFormats = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentOnboardingProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorOnboardingProfiles_MentorUserId",
                table: "MentorOnboardingProfiles",
                column: "MentorUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentOnboardingProfiles_UserId",
                table: "StudentOnboardingProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorOnboardingProfiles");

            migrationBuilder.DropTable(
                name: "StudentOnboardingProfiles");
        }
    }
}
