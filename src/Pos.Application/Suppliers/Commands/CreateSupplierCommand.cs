using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Suppliers.Commands;

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

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proveedor es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El TaxId es requerido.")
            .Length(4, 25).WithMessage("El TaxId debe contener entre 4 y 25 caracteres.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("La calle es requerida.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("La ciudad es requerida.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es requerido.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo electrónico no es válido.");
    }
}

public class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, Result<SupplierDto>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierCommandHandler(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(SupplierDto.FromEntity(supplier));
    }
}
