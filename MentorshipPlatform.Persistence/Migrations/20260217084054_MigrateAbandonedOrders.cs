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
            // ──────────────────────────────────────────────────────────────
            // ORDERS CLEANUP
            // ──────────────────────────────────────────────────────────────

            // 1) Failed orders with no payment attempt → Abandoned
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Abandoned'
                  WHERE ""Status"" = 'Failed'
                    AND ""ProviderPaymentId"" IS NULL;");

            // 2) Very old Pending orders that the expire job somehow missed → Abandoned
            migrationBuilder.Sql(
                @"UPDATE ""Orders""
                  SET ""Status"" = 'Abandoned'
                  WHERE ""Status"" = 'Pending'
                    AND ""CreatedAt"" < NOW() - INTERVAL '1 hour';");

            // ──────────────────────────────────────────────────────────────
            // BOOKINGS CLEANUP
            // ──────────────────────────────────────────────────────────────

            // 3) Cancelled bookings whose order was never paid → Expired
            //    (These were auto-cancelled by CreateBookingCommand stale cleanup)
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
                        AND o.""Status"" IN ('Abandoned', 'Failed', 'Pending')
                        AND o.""ProviderPaymentId"" IS NULL
                    );");

            // 4) Cancelled bookings that have NO order at all → Expired
            //    (Edge case: booking created but order creation failed)
            migrationBuilder.Sql(
                @"UPDATE ""Bookings""
                  SET ""Status"" = 'Expired',
                      ""CancellationReason"" = NULL
                  WHERE ""Status"" = 'Cancelled'
                    AND ""Id"" NOT IN (
                      SELECT o.""ResourceId""
                      FROM ""Orders"" o
                      WHERE o.""Type"" = 'Booking'
                    );");

            // 5) Lingering PendingPayment bookings older than 1 hour → Expired
            //    (Expire job may have missed these)
            migrationBuilder.Sql(
                @"UPDATE ""Bookings""
                  SET ""Status"" = 'Expired'
                  WHERE ""Status"" = 'PendingPayment'
                    AND ""CreatedAt"" < NOW() - INTERVAL '1 hour';");
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
