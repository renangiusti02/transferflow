using Xunit;

namespace TransferFlow.Integration.Tests.Infrastructure;

[CollectionDefinition(
    "PostgreSQL integration tests",
    DisableParallelization = true)]
public sealed class PostgreSqlIntegrationTestCollection
{
}
