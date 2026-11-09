using CardDisputes.Api.Auth;
using CardDisputes.Api.Contracts;
using CardDisputes.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CardDisputes.Api.Controllers;

[ApiController]
[Route("disputes")]
public sealed class DisputesController : ControllerBase
{
    private readonly DisputeService _service;
    private readonly DisputeQueries _queries;

    public DisputesController(DisputeService service, DisputesDbContext db)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(db);
        _service = service;
        _queries = new DisputeQueries(db);
    }

    [HttpGet]
    public async Task<ActionResult<DisputePage>> List(
        [FromQuery] string? status,
        [FromQuery] string? reason,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var page = await _queries.ListAsync(status, reason, limit, cursor, cancellationToken);
        return Ok(page);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDisputeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = Request.Headers.TryGetValue("Idempotency-Key", out var values) && values.Count == 1
            ? values[0]
            : null;

        var command = new CreateDisputeCommand(
            request.CardTxnRef,
            request.Merchant,
            request.AmountMinor,
            request.Currency,
            request.Reason ?? request.ReasonCode);

        var result = await _service.CreateAsync(command, ClientId, key, cancellationToken);

        Response.Headers.Location = $"/disputes/{result.Id}";

        return new ContentResult
        {
            StatusCode = result.StatusCode,
            Content = result.Body,
            ContentType = "application/json; charset=utf-8"
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DisputeView>> Get(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await _service.GetAsync(id, cancellationToken);
        return Ok(dispute);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<DisputeView>> Submit(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await _service.SubmitAsync(id, ClientId, cancellationToken);
        return Ok(dispute);
    }

    [HttpPost("{id:guid}/decide")]
    public async Task<ActionResult<DisputeView>> Decide(
        Guid id,
        [FromBody] DecideRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispute = await _service.DecideAsync(
            id,
            request.Outcome,
            request.Version,
            ClientId,
            cancellationToken);

        return Ok(dispute);
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<DisputeView>> Withdraw(
        Guid id,
        [FromBody] WithdrawRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispute = await _service.WithdrawAsync(
            id,
            request.Version,
            ClientId,
            cancellationToken);

        return Ok(dispute);
    }

    private string ClientId =>
        HttpContext.Items[ApiKeyMiddleware.ClientIdItemKey] as string
        ?? throw new InvalidOperationException("The API key middleware did not set a client id.");
}
