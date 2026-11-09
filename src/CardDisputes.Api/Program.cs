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
