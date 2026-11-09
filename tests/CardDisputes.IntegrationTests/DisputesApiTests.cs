using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class DisputesApiTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DisputesApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthorizedClient();
    }

    [Fact]
    public async Task Create_Returns_201_With_Location_And_Deadline()
    {
        var response = await CreateDisputeAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ApiFactory.ReadAsync<JsonElement>(response);
        var id = body.GetProperty("id").GetGuid();

        Assert.Equal($"/disputes/{id}", response.Headers.Location?.ToString());
        Assert.Equal("OPENED", body.GetProperty("status").GetString());
        Assert.Equal("GOODS_NOT_RECEIVED", body.GetProperty("reason").GetString());
        Assert.Equal(1500L, body.GetProperty("amountMinor").GetInt64());

        var openedAt = DateTimeOffset.Parse(
            body.GetProperty("openedAt").GetString()!,
            CultureInfo.InvariantCulture);
        var deadline = DateOnly.Parse(
            body.GetProperty("responseDeadline").GetString()!,
            CultureInfo.InvariantCulture);

        Assert.Equal(DateOnly.FromDateTime(openedAt.UtcDateTime).AddDays(20), deadline);
    }

    [Fact]
    public async Task Submit_Without_Evidence_Returns_409_IllegalTransition()
    {
        var created = await CreateDisputeAsync();
        var id = (await ApiFactory.ReadAsync<JsonElement>(created)).GetProperty("id").GetGuid();

        var response = await _client.PostAsync($"/disputes/{id}/submit", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/illegal-transition", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Decide_With_Stale_Version_Returns_409_Concurrency()
    {
        var created = await CreateDisputeAsync();
        var createdBody = await ApiFactory.ReadAsync<JsonElement>(created);
        var id = createdBody.GetProperty("id").GetGuid();
        var staleVersion = createdBody.GetProperty("version").GetInt64();

        await _factory.AddEvidenceAsync(id);
        var submitted = await _client.PostAsync($"/disputes/{id}/submit", content: null);
        submitted.EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync(
            $"/disputes/{id}/decide",
            new { outcome = "WON", version = staleVersion });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/concurrency", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Two_Parallel_Decides_One_Wins()
    {
        var created = await CreateDisputeAsync();
        var id = (await ApiFactory.ReadAsync<JsonElement>(created)).GetProperty("id").GetGuid();

        await _factory.AddEvidenceAsync(id);
        var submitted = await _client.PostAsync($"/disputes/{id}/submit", content: null);
        submitted.EnsureSuccessStatusCode();

        var version = (await ApiFactory.ReadAsync<JsonElement>(submitted))
            .GetProperty("version")
            .GetInt64();

        async Task<HttpResponseMessage> DecideAsync(string outcome) =>
            await _client.PostAsJsonAsync(
                $"/disputes/{id}/decide",
                new { outcome, version });

        var responses = await Task.WhenAll(DecideAsync("WON"), DecideAsync("LOST"));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var context = _factory.CreateContext();
        var decideEvents = await context.DisputeEvents.CountAsync(
            disputeEvent => disputeEvent.DisputeId == id
                && disputeEvent.FromStatus == "EVIDENCE_SUBMITTED");

        Assert.Equal(1, decideEvents);
    }

    [Fact]
    public async Task Every_Transition_Writes_One_Event()
    {
        var created = await CreateDisputeAsync();
        var id = (await ApiFactory.ReadAsync<JsonElement>(created)).GetProperty("id").GetGuid();

        await AssertEventCountAsync(id, 1);

        await _factory.AddEvidenceAsync(id);
        var submitted = await _client.PostAsync($"/disputes/{id}/submit", content: null);
        submitted.EnsureSuccessStatusCode();

        await AssertLastEventAsync(id, "OPENED", "EVIDENCE_SUBMITTED");

        var version = (await ApiFactory.ReadAsync<JsonElement>(submitted))
            .GetProperty("version")
            .GetInt64();

        var decided = await _client.PostAsJsonAsync(
            $"/disputes/{id}/decide",
            new { outcome = "WON", version });
        decided.EnsureSuccessStatusCode();

        await AssertLastEventAsync(id, "EVIDENCE_SUBMITTED", "WON");
        await AssertEventCountAsync(id, 3);

        var withdrawCandidate = await CreateDisputeAsync();
        var withdrawBody = await ApiFactory.ReadAsync<JsonElement>(withdrawCandidate);
        var withdrawId = withdrawBody.GetProperty("id").GetGuid();
        var withdrawVersion = withdrawBody.GetProperty("version").GetInt64();

        var withdrawn = await _client.PostAsJsonAsync(
            $"/disputes/{withdrawId}/withdraw",
            new { version = withdrawVersion });
        withdrawn.EnsureSuccessStatusCode();

        await AssertLastEventAsync(withdrawId, "OPENED", "WITHDRAWN");
    }

    [Fact]
    public async Task Validation_Errors_Are_ProblemDetails()
    {
        var zeroAmount = await SendCreateAsync(
            ApiFactory.NewCreateRequest(
                ApiFactory.NewCreateBody(amountMinor: 0),
                Guid.NewGuid().ToString("N")));

        var lowerCurrency = await SendCreateAsync(
            ApiFactory.NewCreateRequest(
                ApiFactory.NewCreateBody(currency: "usd"),
                Guid.NewGuid().ToString("N")));

        var unknownReason = await SendCreateAsync(
            ApiFactory.NewCreateRequest(
                ApiFactory.NewCreateBody(reason: "NOT_A_REASON"),
                Guid.NewGuid().ToString("N")));

        foreach (var response in new[] { zeroAmount, lowerCurrency, unknownReason })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await ApiFactory.ReadAsync<JsonElement>(response);
            Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
        }
    }

    public void Dispose() => _client.Dispose();

    private async Task<HttpResponseMessage> CreateDisputeAsync() =>
        await SendCreateAsync(ApiFactory.NewCreateRequest(
            ApiFactory.NewCreateBody(),
            Guid.NewGuid().ToString("N")));

    private async Task<HttpResponseMessage> SendCreateAsync(HttpRequestMessage request)
    {
        using (request)
        {
            return await _client.SendAsync(request);
        }
    }

    private async Task AssertEventCountAsync(Guid disputeId, int expected)
    {
        await using var context = _factory.CreateContext();
        var count = await context.DisputeEvents.CountAsync(
            disputeEvent => disputeEvent.DisputeId == disputeId);

        Assert.Equal(expected, count);
    }

    private async Task AssertLastEventAsync(Guid disputeId, string from, string to)
    {
        await using var context = _factory.CreateContext();
        var last = await context.DisputeEvents
            .Where(disputeEvent => disputeEvent.DisputeId == disputeId)
            .OrderByDescending(disputeEvent => disputeEvent.OccurredAt)
            .FirstAsync();

        Assert.Equal(from, last.FromStatus);
        Assert.Equal(to, last.ToStatus);
    }
}
