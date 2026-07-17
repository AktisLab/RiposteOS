using RiposteOS.Infrastructure.Documents;

namespace RiposteOS.Tests.TestSupport;

public sealed class TestObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = [];

    public bool IsAvailable { get; set; } = true;

    public async Task PutAsync(string key, Stream content, long contentLength, string contentType, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        _objects[key] = copy.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        return Task.FromResult<Stream>(new MemoryStream(_objects[key], writable: false));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        _objects.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> CanAccessAsync(CancellationToken cancellationToken) => Task.FromResult(IsAvailable);

    public void Reset()
    {
        IsAvailable = true;
        _objects.Clear();
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new ObjectStorageUnavailableException("Object storage is unavailable.");
        }
    }
}
