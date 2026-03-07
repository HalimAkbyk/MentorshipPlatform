using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LibraryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    TemplateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsSharedWithStudents = table.Column<bool>(type: "boolean", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LibraryItems_Users_MentorUserId",
                        column: x => x.MentorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItems_ItemType",
                table: "LibraryItems",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryItems_MentorUserId",
                table: "LibraryItems",
                column: "MentorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryItems");
        }
    }
}
