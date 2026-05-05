using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class AuthSessionRepository(OrchestratorDbContext db) : IAuthSessionRepository
{
    public async Task AddAsync(AuthSession session, CancellationToken cancellationToken) =>
        await db.AuthSessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

    public Task<AuthSession?> FindByHashAsync(string sessionHash, CancellationToken cancellationToken) =>
        db.AuthSessions.FirstOrDefaultAsync(s => s.SessionHash == sessionHash, cancellationToken);

    public Task<AuthSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
        db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
}
