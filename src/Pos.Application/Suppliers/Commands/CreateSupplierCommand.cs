using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Suppliers.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

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
) : ICommand<SupplierDto>;

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

public class CreateSupplierCommandHandler : ICommandHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplierCommandHandler(ISupplierRepository supplierRepository, IUnitOfWork unitOfWork)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SupplierDto> HandleAsync(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);

        bool taxIdExists = await _supplierRepository.ExistsByTaxIdAsync(taxIdVo, null, cancellationToken);
        if (taxIdExists)
        {
            throw new DomainException($"Ya existe un proveedor registrado con el TaxId '{request.TaxId}'.");
        }

        var addressVo = Address.Create(request.Street, request.City, request.ZipCode, request.Country);

        var supplier = Supplier.Create(
            request.Name,
            taxIdVo,
            addressVo,
            request.ContactName,
            request.Email,
            request.Phone);

        await _supplierRepository.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SupplierDto.FromEntity(supplier);
    }
}
