using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Suppliers.Commands;

[RequiresTenant]
[HasPermission(Permissions.Suppliers.Create)]
public record CreateSupplierCommand(
    string Name,
    string TaxId,
    string TaxCountryCode,
    string Street,
    string City,
    string ZipCode,
    string Country,
    string ContactName,
    string Email,
    string Phone
) : ICommand<Result<SupplierDto>>;

public class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateSupplierCommandHandler(
        ISupplierRepository supplierRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<SupplierDto>> HandleAsync(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        TaxId taxIdVo;
        try
        {
            taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SupplierDto>(DomainError.Validation("Supplier.InvalidTaxId", ex.Message));
        }

        bool taxIdExists = await _supplierRepository.ExistsByTaxIdAsync(taxIdVo, null, cancellationToken);
        if (taxIdExists)
        {
            return Result.Fail<SupplierDto>(DomainError.Conflict("Supplier.AlreadyExists", $"Ya existe un proveedor registrado con el TaxId '{request.TaxId}'."));
        }

        Address addressVo;
        Supplier supplier;
        try
        {
            addressVo = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
            supplier = Supplier.Create(
                _tenantContext.RequireTenantId(),
                request.Name,
                taxIdVo,
                addressVo,
                request.ContactName,
                request.Email,
                request.Phone,
                _timeProvider);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SupplierDto>(DomainError.Validation("Supplier.Invalid", ex.Message));
        }

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(SupplierDto.FromEntity(supplier));
    }
}
