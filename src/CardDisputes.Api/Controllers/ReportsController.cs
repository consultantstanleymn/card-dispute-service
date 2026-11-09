using CardDisputes.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CardDisputes.Api.Controllers;

[ApiController]
[Route("reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly DisputesDbContext _db;

    public ReportsController(DisputesDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    [HttpGet("aging")]
    public async Task<ActionResult<AgingReport>> Aging(CancellationToken cancellationToken)
    {
        var clock = HttpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        var query = new AgingReportQuery(_db, clock);
        var report = await query.ExecuteAsync(cancellationToken);
        return Ok(report);
    }
}
