using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransferFlow.Application.Common;

namespace TransferFlow.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private const string IdempotencyKeyIndex =
        "IX_transfers_idempotency_key";

    private readonly TransferFlowDbContext _dbContext;

    public EfUnitOfWork(
        TransferFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _dbContext.ChangeTracker.Clear();

            throw new ConcurrencyConflictException(
                exception);
        }
        catch (DbUpdateException exception)
            when (
                exception.InnerException is PostgresException postgresException &&
                postgresException.SqlState ==
                    PostgresErrorCodes.UniqueViolation &&
                postgresException.ConstraintName ==
                    IdempotencyKeyIndex)
        {
            _dbContext.ChangeTracker.Clear();

            throw new DuplicateIdempotencyKeyException(
                exception);
        }
    }
}