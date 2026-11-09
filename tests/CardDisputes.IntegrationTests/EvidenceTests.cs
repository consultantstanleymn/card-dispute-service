#pragma warning disable CA1707
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using CardDisputes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CardDisputes.IntegrationTests;

public sealed class EvidenceTests : IClassFixture<ApiFactory>, IClassFixture<PostgresFixture>
{
    private readonly ApiFactory _factory;
    private readonly PostgresFixture _postgres;

    public EvidenceTests(ApiFactory factory, PostgresFixture postgres)
    {
        _factory = factory;
        _postgres = postgres;
    }

    [MinioFact]
    public async Task Upload_Stores_Object_And_Row_With_Sha256()
    {
        using var client = CreateAuthorizedClient();
        var disputeId = await CreateDisputeAsync(client);

        var fileBytes = CreatePdfBytes("minio integration");
        var expectedSha = Convert.ToHexString(SHA256.HashData(fileBytes));

        using var response = await PostEvidenceAsync(client, disputeId, fileBytes, "receipt.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var evidence = await ApiFactory.ReadAsync<EvidenceResponse>(response);
        Assert.Equal(expectedSha, evidence.Sha256);
        Assert.Equal(fileBytes.LongLength, evidence.SizeBytes);

        await using var db = _postgres.CreateContext();
        var row = await db.EvidenceFiles.SingleAsync(file => file.DisputeId == disputeId);
        Assert.Equal(expectedSha, row.Sha256);
        Assert.Equal(evidence.Key, row.ObjectKey);

        var s3Client = CreateS3Client();
        var bucket = GetS3Bucket();
        await EnsureBucketAsync(s3Client, bucket);

        using var get = await s3Client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = evidence.Key
        });

        using var memory = new MemoryStream();
        await get.ResponseStream.CopyToAsync(memory);
        var storedSha = Convert.ToHexString(SHA256.HashData(memory.ToArray()));
        Assert.Equal(expectedSha, storedSha);
    }

    [Fact]
    public async Task Exe_Renamed_To_Pdf_Rejected()
    {
        using var client = CreateAuthorizedClient();
        var disputeId = await CreateDisputeAsync(client);

        byte[] exeBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];

        using var response = await PostEvidenceAsync(client, disputeId, exeBytes, "not-a-pdf.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.False(await db.EvidenceFiles.AnyAsync(file => file.DisputeId == disputeId));
    }

    [Fact]
    public async Task Over_5MB_Returns_413()
    {
        using var client = CreateAuthorizedClient();
        var disputeId = await CreateDisputeAsync(client);

        var fileBytes = new byte[FileSignature.MaxFileSizeBytes + 1];
        fileBytes[0] = 0x25;
        fileBytes[1] = 0x50;
        fileBytes[2] = 0x44;
        fileBytes[3] = 0x46;
        fileBytes[4] = 0x2D;

        using var response = await PostEvidenceAsync(client, disputeId, fileBytes, "large.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.False(await db.EvidenceFiles.AnyAsync(file => file.DisputeId == disputeId));
    }

    [Fact]
    public async Task Upload_To_Closed_Dispute_Returns_409()
    {
        using var client = CreateAuthorizedClient();
        var disputeId = await CreateDisputeAsync(client);

        await using (var writeDb = _postgres.CreateContext())
        {
            var dispute = await writeDb.Disputes.SingleAsync(d => d.Id == disputeId);
            dispute.Status = "WITHDRAWN";
            await writeDb.SaveChangesAsync();
        }

        var fileBytes = CreatePdfBytes("closed");
        using var response = await PostEvidenceAsync(client, disputeId, fileBytes, "closed.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.False(await db.EvidenceFiles.AnyAsync(file => file.DisputeId == disputeId));
    }

    [Fact]
    public async Task S3_Down_Returns_503_And_No_Row()
    {
        var closedPort = GetClosedPort();
        using var client = CreateAuthorizedClientWithS3ServiceUrl($"http://127.0.0.1:{closedPort}");
        var disputeId = await CreateDisputeAsync(client);

        var fileBytes = CreatePdfBytes("s3 down");
        using var response = await PostEvidenceAsync(client, disputeId, fileBytes, "down.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.False(await db.EvidenceFiles.AnyAsync(file => file.DisputeId == disputeId));
    }

    private static async Task<HttpResponseMessage> PostEvidenceAsync(
        HttpClient client,
        Guid disputeId,
        byte[] fileBytes,
        string fileName,
        string contentType)
    {
        using var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(fileBytes);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(byteContent, "file", fileName);

        return await client.PostAsync($"/disputes/{disputeId:D}/evidence", content);
    }

    private static async Task<Guid> CreateDisputeAsync(HttpClient client)
    {
        var body = ApiFactory.NewCreateBody();
        using var request = ApiFactory.NewCreateRequest(body, $"evidence-{Guid.NewGuid():N}");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var created = await ApiFactory.ReadAsync<DisputeResponse>(response);
        return created.Id;
    }

    private HttpClient CreateAuthorizedClient() => _factory.CreateAuthorizedClient();

    private HttpClient CreateAuthorizedClientWithS3ServiceUrl(string serviceUrl)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Evidence:S3:ServiceURL"] = serviceUrl
                });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.ApiKey);
        return client;
    }

    private static AmazonS3Client CreateS3Client() =>
        new(new AmazonS3Config
        {
            ServiceURL = "http://127.0.0.1:26660",
            ForcePathStyle = true,
            RegionEndpoint = RegionEndpoint.USEast1
        });

    private static string GetS3Bucket() =>
        Environment.GetEnvironmentVariable("CDS_S3_BUCKET") ?? "disputes";

    private static async Task EnsureBucketAsync(AmazonS3Client client, string bucket)
    {
        if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
        {
            await client.PutBucketAsync(bucket);
        }
    }

    private static byte[] CreatePdfBytes(string marker)
    {
        var markerBytes = Encoding.UTF8.GetBytes(marker);
        var result = new byte[5 + markerBytes.Length];

        result[0] = 0x25;
        result[1] = 0x50;
        result[2] = 0x44;
        result[3] = 0x46;
        result[4] = 0x2D;

        markerBytes.CopyTo(result, 5);
        return result;
    }

    private static int GetClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record DisputeResponse(Guid Id);

    private sealed record EvidenceResponse(string Sha256, long SizeBytes, string Key);

    private sealed class MinioFactAttribute : FactAttribute
    {
        public MinioFactAttribute()
        {
            if (!IsMinioRunning())
            {
                Skip = "MinIO not running on 127.0.0.1:26660";
            }
        }

        private static bool IsMinioRunning()
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("127.0.0.1", 26660).Wait(TimeSpan.FromMilliseconds(200));
            }
            catch
            {
                return false;
            }
        }
    }
}
