using Microsoft.Extensions.AI;
using RiposteOS.Core.Ai;

namespace RiposteOS.Infrastructure.Ai;

public interface IAiChatClientFactory { IChatClient Create(AiProvider provider); }
