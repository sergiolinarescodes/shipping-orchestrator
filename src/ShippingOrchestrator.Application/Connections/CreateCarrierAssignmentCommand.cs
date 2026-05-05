using ShippingOrchestrator.Application.Common;
using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Application.Connections;

public sealed record CreateCarrierAssignmentCommand(
    TenantId TenantId,
    string CarrierCode,
    int Priority,
    IReadOnlyList<string> OriginCountries,
    IReadOnlyList<string> DestinationCountries);

public sealed record CreateCarrierAssignmentResult(Guid AssignmentId);

public static class CreateCarrierAssignmentHandler
{
    public static async Task<CreateCarrierAssignmentResult> Handle(
        CreateCarrierAssignmentCommand command,
        ICarrierAssignmentRepository carrierAssignmentRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var origins = command.OriginCountries.Select(CountryCode.Parse).ToArray();
        var destinations = command.DestinationCountries.Select(CountryCode.Parse).ToArray();

        var assignment = CarrierAssignment.Create(
            command.TenantId, command.CarrierCode, command.Priority, origins, destinations, clock.UtcNow);

        await carrierAssignmentRepository.AddAsync(assignment, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CreateCarrierAssignmentResult(assignment.Id);
    }
}
