using CardDisputes.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardDisputes.Infrastructure;

public sealed class IdempotencyStore
{
    private readonly DisputesDbContext _db;

    public IdempotencyStore(DisputesDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<IdempotencyKey?> FindAsync(string clientId, string key, CancellationToken cancellationToken) =>
        _db.IdempotencyKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.ClientId == clientId && entry.Key == key,
                cancellationToken);

    public void Add(
        string clientId,
        string key,
        string requestHash,
        int statusCode,
        string responseBody,
        DateTimeOffset createdAt)
    {
        _db.IdempotencyKeys.Add(new IdempotencyKey
        {
            ClientId = clientId,
            Key = key,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = createdAt
        });
    }
}
