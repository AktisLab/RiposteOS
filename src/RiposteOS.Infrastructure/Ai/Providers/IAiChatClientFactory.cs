using Microsoft.Extensions.AI;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai.Providers;

public interface IAiChatClientFactory { IChatClient Create(AiProvider provider); }
