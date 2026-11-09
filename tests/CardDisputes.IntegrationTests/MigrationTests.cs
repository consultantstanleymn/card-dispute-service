using Microsoft.EntityFrameworkCore;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class MigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public MigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Migration_Applies_On_Empty_Database()
    {
        await using var context = _fixture.CreateContext();

        await context.Database.MigrateAsync();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "select count(*) from pg_trigger where tgname = 'dispute_events_append_only'";
        var triggerCount = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(1L, triggerCount);
    }
}
