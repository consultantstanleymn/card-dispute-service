using Amazon.S3;
using Amazon.S3.Model;

namespace CardDisputes.Infrastructure;

public sealed class S3HealthCheck
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3HealthCheck(IAmazonS3 client, string bucket)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new ArgumentException("The S3 bucket name is required.", nameof(bucket));
        }

        _client = client;
        _bucket = bucket;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
