using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RiposteOS.Infrastructure.Documents;
using RiposteOS.Infrastructure.Persistence;
using RiposteOS.Tests.TestSupport;

namespace RiposteOS.Tests.Documents;

public sealed class DocumentsFacadeTests
{
    [Fact]
    public async Task UploadRejectsMissingContent()
    {
        await using var dbContext = CreateDbContext();
        var facade = CreateFacade(dbContext, new ObjectStorageOptions());

        await Assert.ThrowsAsync<ArgumentNullException>(() => facade.UploadAsync(
            new DocumentUpload("offre.pdf", "application/pdf", 1, null!),
            CancellationToken.None));
    }

    [Fact]
    public async Task UploadRejectsContentAboveTheConfiguredLimit()
    {
        await using var dbContext = CreateDbContext();
        var facade = CreateFacade(dbContext, new ObjectStorageOptions { MaxDocumentSizeBytes = 1 });
        await using var content = new MemoryStream([1, 2]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => facade.UploadAsync(
            new DocumentUpload("offre.pdf", "application/pdf", 2, content),
            CancellationToken.None));
    }

    [Fact]
    public async Task UploadRejectsNonSeekableContent()
    {
        await using var dbContext = CreateDbContext();
        var facade = CreateFacade(dbContext, new ObjectStorageOptions());
        await using var content = new NonSeekableStream();

        await Assert.ThrowsAsync<ArgumentException>(() => facade.UploadAsync(
            new DocumentUpload("offre.pdf", "application/pdf", 1, content),
            CancellationToken.None));
    }

    private static DocumentsFacade CreateFacade(RiposteDbContext dbContext, ObjectStorageOptions options) =>
        new(dbContext, new TestObjectStorage(), Options.Create(options), TimeProvider.System);

    private static RiposteDbContext CreateDbContext() => new(new DbContextOptionsBuilder<RiposteDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class NonSeekableStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
