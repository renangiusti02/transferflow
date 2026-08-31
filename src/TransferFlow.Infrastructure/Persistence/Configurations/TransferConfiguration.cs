using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransferFlow.Domain;

namespace TransferFlow.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable(
            "transfers",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_transfers_amount_positive",
                    "\"amount\" > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_transfers_source_destination_wallets_different",
                    "\"source_wallet_id\" <> \"destination_wallet_id\"");
            });

        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(transfer => transfer.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transfer => transfer.SourceWalletId)
            .HasColumnName("source_wallet_id")
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transfer => transfer.SourceWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(transfer => transfer.DestinationWalletId)
            .HasColumnName("destination_wallet_id")
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transfer => transfer.DestinationWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(transfer => transfer.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(transfer => transfer.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(transfer => transfer.IdempotencyKey)
            .IsUnique();
    }
}