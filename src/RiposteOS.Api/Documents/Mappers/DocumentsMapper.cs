using Riok.Mapperly.Abstractions;
using RiposteOS.Api.Documents.Dtos;
using RiposteOS.Core.Documents;

namespace RiposteOS.Api.Documents.Mappers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class DocumentsMapper
{
    [MapperIgnoreSource(nameof(StoredDocument.Sha256))]
    [MapperIgnoreSource(nameof(StoredDocument.StorageKey))]
    public static partial DocumentResponse ToDocumentResponse(StoredDocument document);

    [MapperIgnoreSource(nameof(StoredDocument.Sha256))]
    [MapperIgnoreSource(nameof(StoredDocument.StorageKey))]
    public static partial DocumentResponse[] ToDocumentResponses(IEnumerable<StoredDocument> documents);
}
