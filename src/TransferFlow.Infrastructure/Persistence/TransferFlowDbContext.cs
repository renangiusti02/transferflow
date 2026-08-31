using Microsoft.EntityFrameworkCore;
using TransferFlow.Domain;
using TransferFlow.Infrastructure.Persistence.Configurations;

namespace TransferFlow.Infrastructure.Persistence;

public sealed class TransferFlowDbContext : DbContext
{
    public TransferFlowDbContext(
        DbContextOptions<TransferFlowDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new WalletConfiguration());

        modelBuilder.ApplyConfiguration(
            new TransferConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}