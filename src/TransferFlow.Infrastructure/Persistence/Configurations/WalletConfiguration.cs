using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransferFlow.Domain;

namespace TransferFlow.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable(
            "wallets",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_wallets_balance_non_negative",
                    "\"balance\" >= 0");
            });

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(wallet => wallet.Balance)
            .HasColumnName("balance")
            .HasPrecision(18, 2)
            .IsRequired();
    }
}