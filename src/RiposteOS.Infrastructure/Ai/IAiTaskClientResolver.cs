using Microsoft.Extensions.AI;
using RiposteOS.Core.Ai;
namespace RiposteOS.Infrastructure.Ai;

public interface IAiTaskClientResolver { Task<AiTaskClient?> ResolveAsync(AiTask task, CancellationToken cancellationToken); }
public sealed record AiTaskClient(IChatClient Client, Guid ProviderId, string ProviderName, string Model);
