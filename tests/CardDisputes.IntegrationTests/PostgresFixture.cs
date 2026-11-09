using CardDisputes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CardDisputes.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultConnection =
        "Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes";

    public string ConnectionString { get; private set; } = DefaultConnection;

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("CDS_PG");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = DefaultConnection;
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(source).ConnectionString;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "drop schema if exists public cascade; create schema public;";
        await command.ExecuteNonQueryAsync();
    }

    public DisputesDbContext CreateContext() =>
        new(DisputesDbContext.CreateOptions(ConnectionString));

    public Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        return Task.CompletedTask;
    }
}
