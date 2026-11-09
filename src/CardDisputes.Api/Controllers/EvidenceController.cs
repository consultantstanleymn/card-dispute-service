using System.Security.Cryptography;
using CardDisputes.Infrastructure;
using CardDisputes.Infrastructure.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.Api.Controllers;

[ApiController]
[Route("disputes/{disputeId:guid}")]
public sealed class EvidenceController : ControllerBase
{
    private const int MaxMultipartBodyBytes = 6 * 1024 * 1024;

    private readonly DisputesDbContext _db;
    private readonly IEvidenceStore _store;

    public EvidenceController(DisputesDbContext db, IEvidenceStore store)
    {
        _db = db;
        _store = store;
    }

    [HttpPost("evidence")]
    [RequestSizeLimit(MaxMultipartBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxMultipartBodyBytes)]
    public async Task<IActionResult> UploadEvidenceAsync(
        [FromRoute] Guid disputeId,
        [FromForm(Name = "file")] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Problem(
                detail: "The evidence file is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing evidence file");
        }

        var contentType = NormalizeContentType(file.ContentType);
        if (!FileSignature.IsSupportedContentType(contentType))
        {
            return Problem(
                detail: "The uploaded file must be a PDF, PNG, or JPEG with a matching signature.",
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported file type");
        }

        if (file.Length > FileSignature.MaxFileSizeBytes)
        {
            return Problem(
                detail: "The evidence file exceeds the 5 MB limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload too large");
        }

        var dispute = await _db.Disputes
            .AsNoTracking()
            .SingleOrDefaultAsync(dispute => dispute.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            throw new DisputeNotFoundException(disputeId);
        }

        if (!string.Equals(dispute.Status, "OPENED", StringComparison.Ordinal))
        {
            return Problem(
                detail: "Evidence can only be attached to an OPENED dispute.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Dispute is not open",
                type: "/problems/dispute-not-open");
        }

        var evidenceId = Guid.NewGuid();
        var key = $"disputes/{disputeId:N}/{evidenceId:N}";

        await using var source = file.OpenReadStream();
        using var memory = new MemoryStream();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        long sizeBytes = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            sizeBytes += read;
            if (sizeBytes > FileSignature.MaxFileSizeBytes)
            {
                return Problem(
                    detail: "The evidence file exceeds the 5 MB limit.",
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Payload too large");
            }

            sha.AppendData(buffer, 0, read);
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        memory.Position = 0;

        ReadOnlySpan<byte> prefix = memory.TryGetBuffer(out var segment)
            ? segment.AsSpan()
            : memory.ToArray().AsSpan();

        if (!FileSignature.MatchesMagicBytes(contentType, prefix))
        {
            return Problem(
                detail: "The uploaded file type does not match its content.",
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Unsupported file type");
        }

        var sha256 = Convert.ToHexString(sha.GetHashAndReset());

        try
        {
            await _store.PutAsync(key, memory, contentType, sizeBytes, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Problem(
                detail: "The evidence file could not be stored. Try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Evidence storage unavailable");
        }

        var evidence = new EvidenceFile
        {
            Id = evidenceId,
            DisputeId = disputeId,
            FileName = file.FileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            ObjectKey = key,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.EvidenceFiles.Add(evidence);
        await _db.SaveChangesAsync(cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                sha256,
                sizeBytes,
                key
            });
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }
}
