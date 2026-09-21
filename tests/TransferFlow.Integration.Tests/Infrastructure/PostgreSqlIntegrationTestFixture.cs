using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransferFlow.Integration.Tests.Outbox;
using TransferFlow.Infrastructure.Persistence;

namespace TransferFlow.Integration.Tests.Infrastructure;

public sealed class PostgreSqlIntegrationTestFixture
    : IAsyncLifetime
{
    public string ConnectionString { get; }

    public PostgreSqlIntegrationTestFixture()
    {
        var configuration =
            new ConfigurationBuilder()
                .AddUserSecrets<PostgreSqlIntegrationTestFixture>(
                    optional: true)
                .AddEnvironmentVariables()
                .Build();

        ConnectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' was not found.");
    }

    public TransferFlowDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<TransferFlowDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

        return new TransferFlowDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var dbContext =
            CreateDbContext();

        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
