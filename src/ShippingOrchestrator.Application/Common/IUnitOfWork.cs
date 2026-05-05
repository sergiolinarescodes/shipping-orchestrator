namespace ShippingOrchestrator.Application.Common;

/// <summary>
/// Transaction boundary for the orchestrator schema. Implemented by the EF Core DbContext
/// in Infrastructure. Wolverine's EF Core integration calls <see cref="SaveChangesAsync"/>
/// inside the same transaction as the outbox so domain events emit atomically.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
