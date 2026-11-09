namespace CardDisputes.Infrastructure;

public interface IEvidenceStore
{
    Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken);
}
