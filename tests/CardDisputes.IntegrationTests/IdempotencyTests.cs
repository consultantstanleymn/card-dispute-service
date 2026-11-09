using System.Net;
using System.Text.Json;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class IdempotencyTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _client;

    public IdempotencyTests(ApiFactory factory)
    {
        _client = factory.CreateAuthorizedClient();
    }

    [Fact]
    public async Task Create_Same_Key_Same_Body_Returns_Same_Response()
    {
        var key = Guid.NewGuid().ToString("N");
        var body = ApiFactory.NewCreateBody();

        var first = await SendAsync(ApiFactory.NewCreateRequest(body, key));
        var second = await SendAsync(ApiFactory.NewCreateRequest(body, key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await first.Content.ReadAsStringAsync();
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(firstBody, secondBody);
        Assert.Equal(first.Headers.Location?.ToString(), second.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Create_Same_Key_Other_Body_Returns_409()
    {
        var key = Guid.NewGuid().ToString("N");

        var first = await SendAsync(ApiFactory.NewCreateRequest(
            ApiFactory.NewCreateBody(amountMinor: 1500),
            key));

        var second = await SendAsync(ApiFactory.NewCreateRequest(
            ApiFactory.NewCreateBody(amountMinor: 2500),
            key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(second);
        Assert.Equal("/problems/idempotency-conflict", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Create_Without_Idempotency_Key_Returns_400()
    {
        var response = await SendAsync(ApiFactory.NewCreateRequest(
            ApiFactory.NewCreateBody(),
            idempotencyKey: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
    }

    public void Dispose() => _client.Dispose();

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using (request)
        {
            return await _client.SendAsync(request);
        }
    }
}
