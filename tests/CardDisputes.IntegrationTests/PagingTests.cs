using System.Net;
using System.Text.Json;
using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707
#pragma warning disable CA1001

public sealed class PagingTests : IAsyncLifetime
{
    private static readonly DateTimeOffset BaseTime =
        new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly ApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateAuthorizedClient();
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Pages_Do_Not_Skip_Or_Repeat_With_Ties()
    {
        await ResetAsync();

        var rows = new List<DisputeRow>();
        for (var index = 0; index < 20; index++)
        {
            rows.Add(NewRow(BaseTime.AddMinutes(-index)));
        }

        for (var index = 0; index < 10; index++)
        {
            rows.Add(NewRow(BaseTime.AddMinutes(-30)));
        }

        await AddRowsAsync(rows);

        var expected = await OrderedIdsAsync();
        Assert.Equal(30, expected.Count);

        var collected = new List<Guid>();
        string? cursor = null;

        do
        {
            var page = await ReadPageAsync(7, cursor);
            foreach (var item in page.GetProperty("items").EnumerateArray())
            {
                collected.Add(item.GetProperty("id").GetGuid());
            }

            cursor = NextCursor(page);
        }
        while (cursor is not null);

        Assert.Equal(expected, collected);
    }

    [Fact]
    public async Task Row_Inserted_Before_Cursor_Does_Not_Shift_Next_Page()
    {
        await ResetAsync();

        var rows = new List<DisputeRow>();
        for (var index = 0; index < 10; index++)
        {
            rows.Add(NewRow(BaseTime.AddMinutes(-index)));
        }

        await AddRowsAsync(rows);

        var expected = await OrderedIdsAsync();

        var firstPage = await ReadPageAsync(3, cursor: null);
        var firstIds = ReadIds(firstPage);
        Assert.Equal(expected.Take(3), firstIds);

        await AddRowsAsync(new[] { NewRow(BaseTime.AddMinutes(5)) });

        var secondPage = await ReadPageAsync(3, NextCursor(firstPage));
        var secondIds = ReadIds(secondPage);

        Assert.Equal(expected.Skip(3).Take(3), secondIds);
    }

    [Fact]
    public async Task Row_Deleted_Between_Pages_Does_Not_Break_Paging()
    {
        await ResetAsync();

        var rows = new List<DisputeRow>();
        for (var index = 0; index < 10; index++)
        {
            rows.Add(NewRow(BaseTime.AddMinutes(-index)));
        }

        await AddRowsAsync(rows);

        var expected = await OrderedIdsAsync();

        var firstPage = await ReadPageAsync(3, cursor: null);
        Assert.Equal(expected.Take(3), ReadIds(firstPage));

        await using (var context = _factory.CreateContext())
        {
            await context.Disputes
                .Where(dispute => dispute.Id == expected[1])
                .ExecuteDeleteAsync();
        }

        var secondPage = await ReadPageAsync(3, NextCursor(firstPage));

        Assert.Equal(expected.Skip(3).Take(3), ReadIds(secondPage));
    }

    [Fact]
    public async Task Filter_By_Status_And_Reason()
    {
        await ResetAsync();

        await AddRowsAsync(new[]
        {
            NewRow(BaseTime.AddMinutes(-1), "OPENED", "GOODS_NOT_RECEIVED"),
            NewRow(BaseTime.AddMinutes(-2), "OPENED", "GOODS_NOT_RECEIVED"),
            NewRow(BaseTime.AddMinutes(-3), "OPENED", "GOODS_NOT_RECEIVED"),
            NewRow(BaseTime.AddMinutes(-4), "OPENED", "FRAUD_CARD_ABSENT"),
            NewRow(BaseTime.AddMinutes(-5), "OPENED", "FRAUD_CARD_ABSENT"),
            NewRow(BaseTime.AddMinutes(-6), "WON", "GOODS_NOT_RECEIVED"),
            NewRow(BaseTime.AddMinutes(-7), "WON", "GOODS_NOT_RECEIVED"),
            NewRow(BaseTime.AddMinutes(-8), "EVIDENCE_SUBMITTED", "DUPLICATE_CHARGE")
        });

        var openGoods = await ReadPageAsync(
            limit: 100,
            cursor: null,
            status: "OPENED",
            reason: "GOODS_NOT_RECEIVED");

        var items = openGoods.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal("OPENED", item.GetProperty("status").GetString());
            Assert.Equal("GOODS_NOT_RECEIVED", item.GetProperty("reason").GetString());
            Assert.Equal("USD", item.GetProperty("currency").GetString());
            Assert.True(item.GetProperty("version").GetInt64() > 0);
            Assert.Equal(JsonValueKind.String, item.GetProperty("responseDeadline").ValueKind);
        });

        var submitted = await ReadPageAsync(
            limit: 100,
            cursor: null,
            status: "EVIDENCE_SUBMITTED");

        var submittedItems = submitted.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(submittedItems);
        Assert.Equal("DUPLICATE_CHARGE", submittedItems[0].GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Limit_Out_Of_Range_Returns_400(int limit)
    {
        await ResetAsync();

        var response = await _client.GetAsync($"/disputes?limit={limit}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Bad_Cursor_Returns_400()
    {
        await ResetAsync();

        var response = await _client.GetAsync("/disputes?cursor=not-a-real-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
    }

    private async Task ResetAsync()
    {
        await using var context = _factory.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "truncate table dispute_events, evidence_files, idempotency_keys, disputes cascade");
    }

    private static DisputeRow NewRow(
        DateTimeOffset openedAt,
        string status = "OPENED",
        string reason = "GOODS_NOT_RECEIVED") => new()
        {
            Id = Guid.NewGuid(),
            CardTxnRef = $"txn-{Guid.NewGuid():N}",
            Merchant = "Fictional Merchant",
            AmountMinor = 1500,
            Currency = "USD",
            ReasonCode = reason,
            Status = status,
            OpenedAt = openedAt,
            RespondBy = DateOnly.FromDateTime(openedAt.UtcDateTime).AddDays(20),
            CreatedBy = ApiFactory.ClientId
        };

    private async Task AddRowsAsync(IEnumerable<DisputeRow> rows)
    {
        await using var context = _factory.CreateContext();
        context.Disputes.AddRange(rows);
        await context.SaveChangesAsync();
    }

    private async Task<List<Guid>> OrderedIdsAsync()
    {
        await using var context = _factory.CreateContext();
        return await context.Disputes
            .AsNoTracking()
            .OrderByDescending(dispute => dispute.OpenedAt)
            .ThenByDescending(dispute => dispute.Id)
            .Select(dispute => dispute.Id)
            .ToListAsync();
    }

    private async Task<JsonElement> ReadPageAsync(
        int limit,
        string? cursor,
        string? status = null,
        string? reason = null)
    {
        var query = $"/disputes?limit={limit}";
        if (!string.IsNullOrEmpty(status))
        {
            query += $"&status={status}";
        }

        if (!string.IsNullOrEmpty(reason))
        {
            query += $"&reason={reason}";
        }

        if (cursor is not null)
        {
            query += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        var response = await _client.GetAsync(query);
        response.EnsureSuccessStatusCode();

        return await ApiFactory.ReadAsync<JsonElement>(response);
    }

    private static List<Guid> ReadIds(JsonElement page) =>
        page.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

    private static string? NextCursor(JsonElement page)
    {
        var property = page.GetProperty("nextCursor");
        return property.ValueKind == JsonValueKind.Null ? null : property.GetString();
    }
}
