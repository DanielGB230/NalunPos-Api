using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Branches.Commands;

[HasPermission(Permissions.Branches.Create)]
public record CreateBranchCommand(
    string Name,
    string Street,
    string City,
    string ZipCode,
    string Country,
    string PhoneNumber = ""
) : ICommand<Result<BranchDto>>;

[HasPermission(Permissions.Branches.Create)]
public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la sucursal es requerido.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("La calle es requerida.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("La ciudad es requerida.");
    }
}

[HasPermission(Permissions.Branches.Create)]
public class CreateBranchCommandHandler : ICommandHandler<CreateBranchCommand, Result<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBranchCommandHandler(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<BranchDto>> HandleAsync(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        bool exists = await _branchRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (exists)
        {
            return Result.Fail<BranchDto>(DomainError.Conflict("Branch.AlreadyExists", $"Ya existe una sucursal registrada con el nombre '{request.Name}'."));
        }

        Address address;
        Branch branch;
        try
        {
            address = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
            branch = Branch.Create(request.Name, address, request.PhoneNumber);
        }
        catch (DomainException ex)
        {
            return Result.Fail<BranchDto>(DomainError.Validation("Branch.Invalid", ex.Message));
        }

        await _branchRepository.AddAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(BranchDto.FromEntity(branch));
    }
}
