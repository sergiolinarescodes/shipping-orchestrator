using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IMagicLinkTokenRepository
{
    Task AddAsync(MagicLinkToken token, CancellationToken cancellationToken);
    Task<MagicLinkToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
}
