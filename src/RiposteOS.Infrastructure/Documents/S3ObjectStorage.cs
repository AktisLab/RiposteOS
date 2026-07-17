using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace RiposteOS.Infrastructure.Documents;

public sealed class S3ObjectStorage(IAmazonS3 client, IOptions<ObjectStorageOptions> options) : IObjectStorage
{
    public async Task PutAsync(string key, Stream content, long contentLength, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
            }, cancellationToken);
        }
        catch (AmazonClientException exception)
        {
            throw new ObjectStorageUnavailableException("Object storage is unavailable.", exception);
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(options.Value.BucketName, key, cancellationToken);
            return new S3ResponseStream(response.ResponseStream, response);
        }
        catch (AmazonClientException exception)
        {
            throw new ObjectStorageUnavailableException("Object storage is unavailable.", exception);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(options.Value.BucketName, key, cancellationToken);
        }
        catch (AmazonClientException exception)
        {
            throw new ObjectStorageUnavailableException("Object storage is unavailable.", exception);
        }
    }

    public async Task<bool> CanAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = options.Value.BucketName,
                MaxKeys = 1,
            }, cancellationToken);
            return true;
        }
        catch (AmazonClientException)
        {
            return false;
        }
    }

    private sealed class S3ResponseStream(Stream inner, IDisposable response) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
            await base.DisposeAsync();
        }
    }
}
