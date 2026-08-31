using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransferFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "transfers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
            """
            UPDATE transfers
            SET idempotency_key = '__legacy__:' || id::text
            WHERE idempotency_key IS NULL;
            """);

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "transfers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transfers_idempotency_key",
                table: "transfers",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transfers_idempotency_key",
                table: "transfers");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "transfers");
        }
    }
}
