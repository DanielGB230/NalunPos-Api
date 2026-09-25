using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Customers.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

using Pos.Domain.Common;

namespace Pos.Application.Customers.Commands;

[HasPermission(Permissions.Customers.Update)]
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
    string? Country
) : ICommand<Result<CustomerDto>>;

[HasPermission(Permissions.Customers.Update)]
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

[HasPermission(Permissions.Customers.Update)]
public class UpdateCustomerCommandHandler : ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<CustomerDto>> HandleAsync(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (customer == null)
        {
            return Result.Fail<CustomerDto>(DomainError.NotFound("Customer.NotFound", $"No se encontró el cliente con el ID '{request.Id}'."));
        }

        TaxId taxIdVo;
        try
        {
            taxIdVo = TaxId.Create(request.TaxId, request.TaxCountryCode);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CustomerDto>(DomainError.Validation("Customer.InvalidTaxId", ex.Message));
        }

        bool taxIdExists = await _customerRepository.ExistsByTaxIdAsync(taxIdVo, request.Id, cancellationToken);
        if (taxIdExists)
        {
            return Result.Fail<CustomerDto>(DomainError.Conflict("Customer.AlreadyExists", $"Ya existe otro cliente registrado con el TaxId '{request.TaxId}'."));
        }

        try
        {
            Address? addressVo = null;
            if (!string.IsNullOrWhiteSpace(request.Street) && !string.IsNullOrWhiteSpace(request.City) && !string.IsNullOrWhiteSpace(request.Country))
            {
                addressVo = Address.Create(request.Street, request.City, request.ZipCode ?? "", request.Country);
            }

            customer.UpdateDetails(request.FullName, taxIdVo, request.Email, request.Phone, addressVo);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CustomerDto>(DomainError.Validation("Customer.Invalid", ex.Message));
        }

        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CustomerDto.FromEntity(customer));
    }
}
