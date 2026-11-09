using Amazon;
using Amazon.S3;
using CardDisputes.Api.Auth;
using CardDisputes.Api.Errors;
using CardDisputes.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DisputesDbContext>().Database.Migrate();
}

app.UseMiddleware<ExceptionMappingMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

public partial class Program;
