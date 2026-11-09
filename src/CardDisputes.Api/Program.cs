using Amazon;
using Amazon.S3;
using CardDisputes.Api.Auth;
using CardDisputes.Api.Errors;
using CardDisputes.Api.Metrics;
using CardDisputes.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<DisputeMetrics>();
builder.Services.AddSingleton<IDisputeMetrics>(sp => sp.GetRequiredService<DisputeMetrics>());

var connectionString = builder.Configuration.GetConnectionString("Disputes")
    ?? "Host=127.0.0.1;Port=26632;Database=disputes;Username=disputes;Password=disputes";

builder.Services.AddDbContext<DisputesDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IdempotencyStore>();
builder.Services.AddScoped<DisputeService>();

var evidenceS3 = builder.Configuration.GetSection("Evidence:S3");
var evidenceServiceUrl = evidenceS3["ServiceURL"] ?? "http://127.0.0.1:26660";
var evidenceBucket = evidenceS3["Bucket"] ?? "disputes";
var evidenceRegion = evidenceS3["Region"] ?? Amazon.RegionEndpoint.USEast1.SystemName;

builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new AmazonS3Config
{
    ServiceURL = evidenceServiceUrl,
    ForcePathStyle = true,
    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(evidenceRegion)
}));
builder.Services.AddSingleton<IEvidenceStore>(sp => new S3EvidenceStore(
    sp.GetRequiredService<IAmazonS3>(),
    evidenceBucket));
builder.Services.AddSingleton<S3HealthCheck>(sp => new S3HealthCheck(
    sp.GetRequiredService<IAmazonS3>(),
    evidenceBucket));
builder.Services.AddSingleton<S3HealthCheckAdapter>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<DisputesDbContext>("database")
    .AddCheck<S3HealthCheckAdapter>("evidence-s3");

var app = builder.Build();

var migrateOnStart = builder.Configuration.GetValue<bool>("Database:MigrateOnStart");
if ((migrateOnStart && args.Contains("--migrate")) || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DisputesDbContext>().Database.Migrate();
}

app.UseHttpMetrics();
app.UseMiddleware<ExceptionMappingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;

internal sealed class S3HealthCheckAdapter : IHealthCheck
{
    private readonly S3HealthCheck _inner;

    public S3HealthCheckAdapter(S3HealthCheck inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var isHealthy = await _inner.IsHealthyAsync(cancellationToken);
        return isHealthy
            ? HealthCheckResult.Healthy("The evidence bucket is reachable.")
            : HealthCheckResult.Unhealthy("The evidence bucket could not be reached.");
    }
}
