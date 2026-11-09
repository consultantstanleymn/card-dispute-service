using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CardDisputes.IntegrationTests;

#pragma warning disable CA1707

public sealed class ObservabilityTests
{
    [Fact]
    public async Task Metrics_Endpoint_Exposes_Dispute_Counters()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateAuthorizedClient();

        await CreateDisputeAsync(client);

        using var metricsClient = factory.CreateClient();
        var response = await metricsClient.GetAsync("/metrics");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("cds_disputes_opened_total", body);
        Assert.Contains("cds_dispute_transitions_total", body);
        Assert.Contains("cds_evidence_upload_bytes", body);
    }

    [Fact]
    public async Task Transition_Increments_Counter()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateAuthorizedClient();

        var disputeId = await CreateDisputeAsync(client);
        await factory.AddEvidenceAsync(disputeId);

        using var submitResponse = await client.PostAsync($"/disputes/{disputeId}/submit", null);
        submitResponse.EnsureSuccessStatusCode();

        using var metricsClient = factory.CreateClient();
        var metrics = await metricsClient.GetStringAsync("/metrics");

        Assert.Contains(
            "cds_dispute_transitions_total{from=\"OPENED\",to=\"EVIDENCE_SUBMITTED\"}",
            metrics);
    }

    [Fact]
    public async Task Ready_Is_Unhealthy_When_Db_Down()
    {
        using var factory = new BadDatabaseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Logs_Do_Not_Contain_Api_Key()
    {
        const string apiKey = "placeholder-api-key-for-log-check";
        var captured = new CapturingLoggerProvider();
        using var factory = new LogCaptureApiFactory(apiKey, captured);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var response = await client.GetAsync($"/disputes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var logs = string.Join('\n', captured.Messages);
        Assert.DoesNotContain(apiKey, logs);
    }

    private static async Task<Guid> CreateDisputeAsync(HttpClient client)
    {
        var body = ApiFactory.NewCreateBody();
        using var request = ApiFactory.NewCreateRequest(body, $"idem-{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private sealed class BadDatabaseApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Disputes"] =
                        "Host=127.0.0.1;Port=1;Database=disputes;Username=disputes;Password=disputes;Timeout=1",
                    ["ApiKeys:test-key"] = "client-test"
                });
            });
        }
    }

    private sealed class LogCaptureApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _apiKey;
        private readonly CapturingLoggerProvider _provider;

        public LogCaptureApiFactory(string apiKey, CapturingLoggerProvider provider)
        {
            _apiKey = apiKey;
            _provider = provider;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Disputes"] =
                        "Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes",
                    ["ApiKeys:" + _apiKey] = "client-test"
                });
            });

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(_provider);
            });
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _provider;

        public CapturingLogger(CapturingLoggerProvider provider)
        {
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Messages.Enqueue(formatter(state, exception));
            if (exception is not null)
            {
                _provider.Messages.Enqueue(exception.ToString());
            }
        }
    }
}
