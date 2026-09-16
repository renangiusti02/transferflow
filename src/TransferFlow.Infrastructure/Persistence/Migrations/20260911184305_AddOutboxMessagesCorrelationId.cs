using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransferFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessagesCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE outbox_messages
                SET correlation_id =
                    (payload ->> 'TransferId')::uuid
                WHERE type = 'TransferCompleted';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "correlation_id",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "outbox_messages");
        }
    }
}
