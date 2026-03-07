using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "SessionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "SessionPlans",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Curriculums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "Curriculums",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "Assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "Assignments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_IsTemplate",
                table: "SessionPlans",
                column: "IsTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculums_IsTemplate",
                table: "Curriculums",
                column: "IsTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_IsTemplate",
                table: "Assignments",
                column: "IsTemplate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SessionPlans_IsTemplate",
                table: "SessionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Curriculums_IsTemplate",
                table: "Curriculums");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_IsTemplate",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "SessionPlans");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "SessionPlans");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Curriculums");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "Curriculums");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "Assignments");
        }
    }
}
