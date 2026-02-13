using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerOfferingAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityTemplateId",
                table: "Offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "AvailabilitySlots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offerings_AvailabilityTemplateId",
                table: "Offerings",
                column: "AvailabilityTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_TemplateId",
                table: "AvailabilitySlots",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Offerings_AvailabilityTemplates_AvailabilityTemplateId",
                table: "Offerings",
                column: "AvailabilityTemplateId",
                principalTable: "AvailabilityTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Mevcut slot'ları mentor'un default template'ine bağla
            migrationBuilder.Sql(@"
                UPDATE ""AvailabilitySlots"" AS s
                SET ""TemplateId"" = t.""Id""
                FROM ""AvailabilityTemplates"" AS t
                WHERE t.""MentorUserId"" = s.""MentorUserId"" AND t.""IsDefault"" = true;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Offerings_AvailabilityTemplates_AvailabilityTemplateId",
                table: "Offerings");

            migrationBuilder.DropIndex(
                name: "IX_Offerings_AvailabilityTemplateId",
                table: "Offerings");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_TemplateId",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "AvailabilityTemplateId",
                table: "Offerings");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "AvailabilitySlots");
        }
    }
}
