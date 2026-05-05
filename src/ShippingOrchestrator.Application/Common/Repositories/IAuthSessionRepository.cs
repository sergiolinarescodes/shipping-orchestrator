using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);
    Task<AuthSession?> FindByHashAsync(string sessionHash, CancellationToken cancellationToken);
    Task<AuthSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken);
}
