using Amazon.S3;
using Amazon.S3.Model;

namespace CardDisputes.Infrastructure;

public sealed class S3EvidenceStore : IEvidenceStore
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3EvidenceStore(IAmazonS3 client, string bucket)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new ArgumentException("The S3 bucket name is required.", nameof(bucket));
        }

        _client = client;
        _bucket = bucket;
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            AutoCloseStream = false,
            AutoResetStreamPosition = false
        };

        request.Headers.ContentLength = sizeBytes;

        await _client.PutObjectAsync(request, cancellationToken);
    }
}
