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
            // 1) Reclassify existing Failed orders that never had a payment attempt
            // (ProviderPaymentId is NULL → user opened Iyzico popup but closed without paying)
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Abandoned'
                  WHERE ""Status"" = 'Failed'
                    AND ""ProviderPaymentId"" IS NULL;");

            // 2) Reclassify Cancelled bookings that were auto-cancelled due to unpaid stale bookings
            // These are bookings whose related order was never paid (Abandoned/Failed with no payment)
            // CancellationReason contains 'ödenmemiş' or the booking has no Paid order
            migrationBuilder.Sql(
                @"UPDATE ""Bookings""
                  SET ""Status"" = 'Expired',
                      ""CancellationReason"" = NULL
                  WHERE ""Status"" = 'Cancelled'
                    AND ""Id"" IN (
                      SELECT b.""Id""
                      FROM ""Bookings"" b
                      INNER JOIN ""Orders"" o ON o.""ResourceId"" = b.""Id"" AND o.""Type"" = 'Booking'
                      WHERE b.""Status"" = 'Cancelled'
                        AND o.""Status"" IN ('Abandoned', 'Failed')
                        AND o.""ProviderPaymentId"" IS NULL
                    );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Abandoned orders back to Failed
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Failed'
                  WHERE ""Status"" = 'Abandoned';");

            // Note: Cannot reliably revert Expired→Cancelled for bookings
            // because we can't distinguish between originally Expired and migrated ones
        }
    }
}
