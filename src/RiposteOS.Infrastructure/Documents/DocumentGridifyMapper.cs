using Gridify;
using RiposteOS.Core.Documents;

namespace RiposteOS.Infrastructure.Documents;

internal sealed class DocumentGridifyMapper : GridifyMapper<StoredDocument>
{
    public DocumentGridifyMapper()
    {
        AddMap("id", document => document.Id);
        AddMap("originalFileName", document => document.OriginalFileName);
        AddMap("contentType", document => document.ContentType);
        AddMap("size", document => document.Size);
        AddMap("createdAt", document => document.CreatedAt);
    }
}
