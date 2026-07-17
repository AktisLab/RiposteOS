namespace RiposteOS.Infrastructure.Documents;

public sealed class ObjectStorageUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
