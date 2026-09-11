using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransferFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingOutboxIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_occurred_at_utc",
                table: "outbox_messages",
                column: "occurred_at_utc",
                filter: "\"processed_at_utc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_occurred_at_utc",
                table: "outbox_messages");
        }
    }
}
