using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Application.Common.Repositories;

public interface IAccountRepository
{
    Task<Account?> FindByIdAsync(AccountId id, CancellationToken cancellationToken);
    Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(Account account, CancellationToken cancellationToken);
}
