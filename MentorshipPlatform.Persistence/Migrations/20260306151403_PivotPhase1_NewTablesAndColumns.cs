using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PivotPhase1_NewTablesAndColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InstructorAssignedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructorStatus",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CreditCost",
                table: "GroupClasses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecordingUrl",
                table: "GroupClasses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstructorId",
                table: "Courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditTransactionId",
                table: "CourseEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditTransactionId",
                table: "ClassEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "ClassEnrollments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Direct");

            migrationBuilder.AddColumn<Guid>(
                name: "CreditTransactionId",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstructorAccrualParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrivateLessonRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GroupLessonRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VideoContentRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusThresholdLessons = table.Column<int>(type: "integer", nullable: true),
                    BonusPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorAccrualParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorAccrualParameters_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstructorAccruals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrivateLessonCount = table.Column<int>(type: "integer", nullable: false),
                    PrivateLessonUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GroupLessonCount = table.Column<int>(type: "integer", nullable: false),
                    GroupLessonUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VideoContentCount = table.Column<int>(type: "integer", nullable: false),
                    VideoUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BonusDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAccrual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorAccruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorAccruals_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstructorPerformanceSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalPrivateLessons = table.Column<int>(type: "integer", nullable: false),
                    TotalGroupLessons = table.Column<int>(type: "integer", nullable: false),
                    TotalVideoViews = table.Column<int>(type: "integer", nullable: false),
                    TotalLiveDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalVideoWatchMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalStudentsServed = table.Column<int>(type: "integer", nullable: false),
                    TotalCreditsConsumed = table.Column<int>(type: "integer", nullable: false),
                    TotalDirectRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCreditRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrivateLessonDemandRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GroupLessonFillRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorPerformanceSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorPerformanceSummaries_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstructorSessionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructorSessionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstructorSessionLogs_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstructorSessionLogs_VideoParticipants_VideoParticipantId",
                        column: x => x.VideoParticipantId,
                        principalTable: "VideoParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrivateLessonCredits = table.Column<int>(type: "integer", nullable: false),
                    GroupLessonCredits = table.Column<int>(type: "integer", nullable: false),
                    VideoAccessCredits = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoWatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WatchEndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WatchedDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    VideoDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoWatchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoWatchLogs_CourseLectures_LectureId",
                        column: x => x.LectureId,
                        principalTable: "CourseLectures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoWatchLogs_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoWatchLogs_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoWatchLogs_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackagePurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagePurchases_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagePurchases_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackagePurchases_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalCredits = table.Column<int>(type: "integer", nullable: false),
                    UsedCredits = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCredits_PackagePurchases_PackagePurchaseId",
                        column: x => x.PackagePurchaseId,
                        principalTable: "PackagePurchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentCredits_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentCreditId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InstructorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_StudentCredits_StudentCreditId",
                        column: x => x.StudentCreditId,
                        principalTable: "StudentCredits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditTransactions_Users_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_InstructorId",
                table: "Courses",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_InstructorId",
                table: "CreditTransactions",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditTransactions_StudentCreditId",
                table: "CreditTransactions",
                column: "StudentCreditId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorAccrualParameters_InstructorId_IsActive",
                table: "InstructorAccrualParameters",
                columns: new[] { "InstructorId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InstructorAccruals_InstructorId",
                table: "InstructorAccruals",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorAccruals_Status",
                table: "InstructorAccruals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorPerformanceSummaries_InstructorId_PeriodType_Peri~",
                table: "InstructorPerformanceSummaries",
                columns: new[] { "InstructorId", "PeriodType", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstructorSessionLogs_InstructorId",
                table: "InstructorSessionLogs",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorSessionLogs_SessionId",
                table: "InstructorSessionLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstructorSessionLogs_VideoParticipantId",
                table: "InstructorSessionLogs",
                column: "VideoParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePurchases_OrderId",
                table: "PackagePurchases",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePurchases_PackageId",
                table: "PackagePurchases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePurchases_StudentId",
                table: "PackagePurchases",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_IsActive",
                table: "Packages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCredits_PackagePurchaseId",
                table: "StudentCredits",
                column: "PackagePurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCredits_StudentId_CreditType",
                table: "StudentCredits",
                columns: new[] { "StudentId", "CreditType" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchLogs_CourseId",
                table: "VideoWatchLogs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchLogs_InstructorId",
                table: "VideoWatchLogs",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchLogs_LectureId_StudentId",
                table: "VideoWatchLogs",
                columns: new[] { "LectureId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoWatchLogs_StudentId",
                table: "VideoWatchLogs",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Courses_Users_InstructorId",
                table: "Courses",
                column: "InstructorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Courses_Users_InstructorId",
                table: "Courses");

            migrationBuilder.DropTable(
                name: "CreditTransactions");

            migrationBuilder.DropTable(
                name: "InstructorAccrualParameters");

            migrationBuilder.DropTable(
                name: "InstructorAccruals");

            migrationBuilder.DropTable(
                name: "InstructorPerformanceSummaries");

            migrationBuilder.DropTable(
                name: "InstructorSessionLogs");

            migrationBuilder.DropTable(
                name: "VideoWatchLogs");

            migrationBuilder.DropTable(
                name: "StudentCredits");

            migrationBuilder.DropTable(
                name: "PackagePurchases");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropIndex(
                name: "IX_Courses_InstructorId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "InstructorAssignedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InstructorStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreditCost",
                table: "GroupClasses");

            migrationBuilder.DropColumn(
                name: "RecordingUrl",
                table: "GroupClasses");

            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "CreditTransactionId",
                table: "CourseEnrollments");

            migrationBuilder.DropColumn(
                name: "CreditTransactionId",
                table: "ClassEnrollments");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "ClassEnrollments");

            migrationBuilder.DropColumn(
                name: "CreditTransactionId",
                table: "Bookings");
        }
    }
}
