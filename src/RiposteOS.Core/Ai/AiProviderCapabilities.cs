namespace RiposteOS.Core.Ai;

[Flags]
public enum AiProviderCapabilities
{
    None = 0,
    Chat = 1,
    Embedding = 2,
    ToolCalling = 4,
    Reasoning = 8,
}
