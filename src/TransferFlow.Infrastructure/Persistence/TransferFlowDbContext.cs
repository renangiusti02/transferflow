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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new WalletConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}