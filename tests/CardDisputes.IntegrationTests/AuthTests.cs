using System.Net;
using System.Text.Json;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class AuthTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly HttpClient _anonymous;

    public AuthTests(ApiFactory factory)
    {
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Missing_Api_Key_Returns_401()
    {
        var response = await _anonymous.GetAsync($"/disputes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/unauthorized", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Unknown_Api_Key_Returns_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/disputes/{Guid.NewGuid()}");
        request.Headers.Add("X-Api-Key", "not-a-real-key");

        var response = await _anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ApiFactory.ReadAsync<JsonElement>(response);
        Assert.Equal("/problems/unauthorized", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Health_Needs_No_Key()
    {
        var response = await _anonymous.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => _anonymous.Dispose();
}
