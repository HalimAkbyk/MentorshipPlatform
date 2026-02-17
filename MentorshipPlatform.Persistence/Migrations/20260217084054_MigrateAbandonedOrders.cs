using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorshipPlatform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAbandonedOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reclassify existing Failed orders that never had a payment attempt
            // (ProviderPaymentId is NULL → user opened Iyzico popup but closed without paying)
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Abandoned'
                  WHERE ""Status"" = 'Failed'
                    AND ""ProviderPaymentId"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Abandoned orders back to Failed
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Failed'
                  WHERE ""Status"" = 'Abandoned';");
        }
    }
}
