using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransferFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(
            MigrationBuilder migrationBuilder)
        {
            // xmin is a PostgreSQL system column.
            // No physical column needs to be created.
        }

        /// <inheritdoc />
        protected override void Down(
            MigrationBuilder migrationBuilder)
        {
            // No physical column was created.
        }
    }
}
