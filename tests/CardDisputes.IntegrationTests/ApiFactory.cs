using System.Net.Http.Json;
using CardDisputes.Infrastructure;
using CardDisputes.Infrastructure.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-key";
    public const string ClientId = "client-test";

    private const string DefaultConnection =
        "Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes";

    public string ConnectionString { get; } = new NpgsqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("CDS_PG") is { Length: > 0 } configured
            ? configured
            : DefaultConnection).ConnectionString;

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        return client;
    }

    public DisputesDbContext CreateContext() => new(DisputesDbContext.CreateOptions(ConnectionString));

    public async Task AddEvidenceAsync(Guid disputeId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.EvidenceFiles.Add(new EvidenceFile
        {
            Id = Guid.NewGuid(),
            DisputeId = disputeId,
            FileName = "receipt.pdf",
            ContentType = "application/pdf",
            SizeBytes = 32,
            Sha256 = new string('a', 64),
            ObjectKey = $"evidence/{disputeId:N}/receipt.pdf",
            UploadedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public static object NewCreateBody(
        string? cardTxnRef = null,
        long amountMinor = 1500,
        string? currency = "USD",
        string? reason = "GOODS_NOT_RECEIVED",
        string merchant = "Fictional Merchant") => new
        {
            cardTxnRef = cardTxnRef ?? $"txn-{Guid.NewGuid():N}",
            merchant,
            amountMinor,
            currency,
            reason
        };

    public static HttpRequestMessage NewCreateRequest(object body, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/disputes");
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        request.Content = JsonContent.Create(body);
        return request;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException("The response body was empty.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Disputes"] = ConnectionString,
                ["ApiKeys:" + ApiKey] = ClientId
            });
        });
    }
}
