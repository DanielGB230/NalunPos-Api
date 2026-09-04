using Pos.Domain.Common;

namespace Pos.Domain.DomainEvents;

public record SupplierCreatedDomainEvent(Guid SupplierId, string Name, string TaxId, DateTime OccurredOnUtc) : IDomainEvent;

public record SupplierUpdatedDomainEvent(Guid SupplierId, string Name, DateTime OccurredOnUtc) : IDomainEvent;

public record SupplierDeactivatedDomainEvent(Guid SupplierId, DateTime OccurredOnUtc) : IDomainEvent;
