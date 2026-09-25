using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Customers.Commands;

[HasPermission(Permissions.Customers.Create)]
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
) : ICommand<Result<CustomerDto>>;

[HasPermission(Permissions.Customers.Create)]
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

[HasPermission(Permissions.Customers.Create)]
public class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<CustomerDto>> HandleAsync(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        TaxId taxIdVo;
        try
        {
            taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CustomerDto>(DomainError.Validation("Customer.InvalidTaxId", ex.Message));
        }

        bool taxIdExists = await _customerRepository.ExistsByTaxIdAsync(taxIdVo, null, cancellationToken);
        if (taxIdExists)
        {
            return Result.Fail<CustomerDto>(DomainError.Conflict("Customer.AlreadyExists", $"Ya existe un cliente registrado con el TaxId '{request.TaxId}'."));
        }

        Address? addressVo = null;
        Customer customer;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Street) && !string.IsNullOrWhiteSpace(request.City) && !string.IsNullOrWhiteSpace(request.Country))
            {
                addressVo = Address.Create(request.Street, request.City, request.ZipCode ?? "", request.Country);
            }

            customer = Customer.Create(
                request.FullName,
                taxIdVo,
                request.Email,
                request.Phone,
                addressVo);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CustomerDto>(DomainError.Validation("Customer.Invalid", ex.Message));
        }

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CustomerDto.FromEntity(customer));
    }
}
