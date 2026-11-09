using CardDisputes.Infrastructure;

namespace CardDisputes.UnitTests;

public sealed class InMemoryEvidenceStore : IEvidenceStore
{
    public Dictionary<string, StoredEvidence> Stored { get; } = new(StringComparer.Ordinal);

    public Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var memory = new MemoryStream();
        content.CopyTo(memory);
        Stored[key] = new StoredEvidence(contentType, memory.ToArray(), sizeBytes);
        return Task.CompletedTask;
    }
}

public sealed record StoredEvidence(string ContentType, byte[] Content, long SizeBytes);
