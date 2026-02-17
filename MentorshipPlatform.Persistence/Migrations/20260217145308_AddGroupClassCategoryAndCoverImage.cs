using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupClassCategoryAndCoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClassEnrollments_GroupClasses_GroupClassId",
                table: "ClassEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ClassEnrollments_GroupClassId",
                table: "ClassEnrollments");

            migrationBuilder.DropColumn(
                name: "GroupClassId",
                table: "ClassEnrollments");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "GroupClasses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "GroupClasses",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "GroupClasses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "GroupClasses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "GroupClasses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "GroupClasses");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "GroupClasses");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "GroupClasses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "GroupClasses",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "GroupClasses",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupClassId",
                table: "ClassEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassEnrollments_GroupClassId",
                table: "ClassEnrollments",
                column: "GroupClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClassEnrollments_GroupClasses_GroupClassId",
                table: "ClassEnrollments",
                column: "GroupClassId",
                principalTable: "GroupClasses",
                principalColumn: "Id");
        }
    }
}
