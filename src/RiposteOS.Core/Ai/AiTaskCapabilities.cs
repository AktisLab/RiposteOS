namespace RiposteOS.Core.Ai;

public static class AiTaskCapabilities
{
    public static AiProviderCapabilities RequiredBy(AiTask task) => task switch
    {
        AiTask.DocumentClassification => AiProviderCapabilities.Chat,
        AiTask.ConsultationChat => AiProviderCapabilities.Chat | AiProviderCapabilities.ToolCalling,
        AiTask.DocumentEmbedding => AiProviderCapabilities.Embedding,
        _ => throw new ArgumentOutOfRangeException(nameof(task)),
    };
}
