using Microsoft.EntityFrameworkCore;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(OrchestratorDbContext db) : IAccountRepository
{
    public Task<Account?> FindByIdAsync(AccountId id, CancellationToken cancellationToken) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = Account.NormalizeEmail(email);
        return db.Accounts.FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await db.Accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
}
