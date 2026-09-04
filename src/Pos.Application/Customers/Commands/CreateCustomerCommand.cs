using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Customers.Commands;

public record CreateCustomerCommand(
    string FullName,
    string TaxId,
    string TaxCountryCode,
    string Email = "",
    string Phone = "",
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null
) : ICommand<CustomerDto>;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre del cliente es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("El TaxId es requerido.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo electrónico no es válido.");
    }
}

public class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CustomerDto> HandleAsync(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);

        bool taxIdExists = await _customerRepository.ExistsByTaxIdAsync(taxIdVo, null, cancellationToken);
        if (taxIdExists)
        {
            throw new DomainException($"Ya existe un cliente registrado con el TaxId '{request.TaxId}'.");
        }

        Address? addressVo = null;
        if (!string.IsNullOrWhiteSpace(request.Street) && !string.IsNullOrWhiteSpace(request.City) && !string.IsNullOrWhiteSpace(request.Country))
        {
            addressVo = Address.Create(request.Street, request.City, request.ZipCode ?? "", request.Country);
        }

        var customer = Customer.Create(
            request.FullName,
            taxIdVo,
            request.Email,
            request.Phone,
            addressVo);

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CustomerDto.FromEntity(customer);
    }
}
