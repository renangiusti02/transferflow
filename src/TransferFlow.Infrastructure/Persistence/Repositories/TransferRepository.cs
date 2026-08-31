using Microsoft.EntityFrameworkCore;
using TransferFlow.Application.Transfers;
using TransferFlow.Domain;

namespace TransferFlow.Infrastructure.Persistence.Repositories;

public sealed class TransferRepository : ITransferRepository
{
    private readonly TransferFlowDbContext _dbContext;

    public TransferRepository(
        TransferFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(
        Transfer transfer)
    {
        _dbContext.Transfers.Add(transfer);
    }

    public async Task<Transfer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Transfer?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transfers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                transfer => transfer.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }
}