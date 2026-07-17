using System.Security.Cryptography;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure.Documents;

namespace RiposteOS.Tests.Documents;

public sealed class SeaweedFsObjectStorageTests : IAsyncLifetime
{
    private const string AccessKey = "riposteos";
    private const string SecretKey = "riposteos-local-secret";
    private const string BucketName = "riposteos-documents-tests";
    private readonly IContainer _container = new ContainerBuilder("chrislusf/seaweedfs:4.29")
        .WithEnvironment("AWS_ACCESS_KEY_ID", AccessKey)
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", SecretKey)
        .WithEnvironment("S3_BUCKET", BucketName)
        .WithPortBinding(8333, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("All enabled components are running and ready to use:"))
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task StreamsBytesThroughSeaweedFsWithoutChangingTheirSha256()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") is not null
            ? "host.docker.internal"
            : "localhost";
        var endpoint = $"http://{host}:{_container.GetMappedPublicPort(8333)}";
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(AccessKey, SecretKey),
            new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ForcePathStyle = true,
                ServiceURL = endpoint,
            });
        var storage = new S3ObjectStorage(
            client,
            Options.Create(new ObjectStorageOptions
            {
                BucketName = BucketName,
                Region = "us-east-1",
                ServiceUrl = endpoint,
                ForcePathStyle = true,
            }));
        var source = "%PDF-1.7\nSeaweedFS réel"u8.ToArray();
        const string key = "documents/11111111111111111111111111111111/content";

        await using var upload = new MemoryStream(source, writable: false);
        await storage.PutAsync(key, upload, source.Length, "application/pdf", CancellationToken.None);
        await using var downloaded = await storage.OpenReadAsync(key, CancellationToken.None);
        using var copy = new MemoryStream();
        await downloaded.CopyToAsync(copy);

        Assert.True(await storage.CanAccessAsync(CancellationToken.None));
        Assert.Equal(SHA256.HashData(source), SHA256.HashData(copy.ToArray()));

        await storage.DeleteAsync(key, CancellationToken.None);
    }
}
