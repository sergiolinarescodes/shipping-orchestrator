using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class MagicLinkTokenRepository(OrchestratorDbContext db) : IMagicLinkTokenRepository
{
    public async Task AddAsync(MagicLinkToken token, CancellationToken cancellationToken) =>
        await db.MagicLinkTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public Task<MagicLinkToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.MagicLinkTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
}
