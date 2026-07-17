namespace RiposteOS.Infrastructure.Documents;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, long contentLength, string contentType, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Task<bool> CanAccessAsync(CancellationToken cancellationToken);
}
