using Microsoft.EntityFrameworkCore;
using Npgsql;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Infrastructure.Persistence.Configurations;
using Testcontainers.PostgreSql;

namespace RiposteOS.Tests.TestSupport;

[CollectionDefinition(Name)]
public sealed class PostgreSqlTestGroup : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("riposteos_tests")
        .WithUsername("riposteos")
        .WithPassword("riposteos")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<string> CreateDatabaseAsync()
    {
        var databaseName = $"riposteos_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "postgres",
        }.ConnectionString;

        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName,
        }.ConnectionString;
        await using var dbContext = CreateContext(connectionString);
        await dbContext.Database.MigrateAsync();
        return connectionString;
    }

    public static RiposteDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<RiposteDbContext>()
            .UseNpgsql(
                connectionString,
                options => options.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    DatabaseSchemas.Infrastructure))
            .Options);
}
