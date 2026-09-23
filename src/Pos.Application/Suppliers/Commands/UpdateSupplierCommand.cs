using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Suppliers.Commands;

public record UpdateSupplierCommand(
    Guid Id,
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

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del proveedor es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El TaxId es requerido.");
    }
}

public class UpdateSupplierCommandHandler : ICommandHandler<UpdateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplierCommandHandler(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<SupplierDto>> HandleAsync(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _supplierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (supplier == null)
        {
            return Result.Fail<SupplierDto>(DomainError.NotFound("Supplier.NotFound", $"No se encontró el proveedor con el ID '{request.Id}'."));
        }

        TaxId taxIdVo;
        try
        {
            taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SupplierDto>(DomainError.Validation("Supplier.InvalidTaxId", ex.Message));
        }

        bool taxIdExists = await _supplierRepository.ExistsByTaxIdAsync(taxIdVo, request.Id, cancellationToken);
        if (taxIdExists)
        {
            return Result.Fail<SupplierDto>(DomainError.Conflict("Supplier.AlreadyExists", $"Ya existe otro proveedor registrado con el TaxId '{request.TaxId}'."));
        }

        try
        {
            var addressVo = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
            supplier.UpdateDetails(
                request.Name,
                taxIdVo,
                addressVo,
                request.ContactName,
                request.Email,
                request.Phone);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SupplierDto>(DomainError.Validation("Supplier.Invalid", ex.Message));
        }

        _supplierRepository.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(SupplierDto.FromEntity(supplier));
    }
}
