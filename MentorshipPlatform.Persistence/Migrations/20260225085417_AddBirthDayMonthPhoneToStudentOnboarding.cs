using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthDayMonthPhoneToStudentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BirthDay",
                table: "StudentOnboardingProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthMonth",
                table: "StudentOnboardingProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "StudentOnboardingProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDay",
                table: "StudentOnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "BirthMonth",
                table: "StudentOnboardingProfiles");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "StudentOnboardingProfiles");
        }
    }
}
