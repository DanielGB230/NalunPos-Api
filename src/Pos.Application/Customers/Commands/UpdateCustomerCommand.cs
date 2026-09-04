using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Customers.Commands;

public record UpdateCustomerCommand(
    Guid Id,
    string FullName,
    string TaxId,
    string TaxCountryCode,
    string Email,
    string Phone,
    string? Street,
    string? City,
    string? ZipCode,
    string? Country,
    bool IsActive
) : ICommand<CustomerDto>;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del cliente es requerido.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre del cliente es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El TaxId es requerido.");
    }
}

public class UpdateCustomerCommandHandler : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CustomerDto> HandleAsync(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CustomerNotFoundException(request.Id);

        var taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);
        bool taxIdExists = await _customerRepository.ExistsByTaxIdAsync(taxIdVo, request.Id, cancellationToken);
        if (taxIdExists)
        {
            throw new DomainException($"Ya existe otro cliente registrado con el TaxId '{request.TaxId}'.");
        }

        Address? addressVo = null;
        if (!string.IsNullOrWhiteSpace(request.Street) && !string.IsNullOrWhiteSpace(request.City) && !string.IsNullOrWhiteSpace(request.Country))
        {
            addressVo = Address.Create(request.Street, request.City, request.ZipCode ?? "", request.Country);
        }

        customer.UpdateDetails(request.FullName, taxIdVo, request.Email, request.Phone, addressVo);

        if (request.IsActive && !customer.IsActive)
        {
            customer.Activate();
        }
        else if (!request.IsActive && customer.IsActive)
        {
            customer.Deactivate();
        }

        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerDto.FromEntity(customer);
    }
}
