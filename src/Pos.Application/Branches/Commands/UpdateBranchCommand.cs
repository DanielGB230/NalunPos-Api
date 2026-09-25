using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Branches.Commands;

[HasPermission(Permissions.Branches.Update)]
public record UpdateBranchCommand(
    Guid Id,
    string Name,
    string Street,
    string City,
    string ZipCode,
    string Country,
    string PhoneNumber
) : ICommand<Result<BranchDto>>;

[HasPermission(Permissions.Branches.Update)]
public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la sucursal es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es requerido.");

        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("La calle es requerida.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("La ciudad es requerida.");
    }
}

[HasPermission(Permissions.Branches.Update)]
public class UpdateBranchCommandHandler : ICommandHandler<UpdateBranchCommand, Result<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBranchCommandHandler(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<BranchDto>> HandleAsync(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (branch == null)
        {
            return Result.Fail<BranchDto>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.Id}'."));
        }

        bool exists = await _branchRepository.ExistsByNameAsync(request.Name, request.Id, cancellationToken);
        if (exists)
        {
            return Result.Fail<BranchDto>(DomainError.Conflict("Branch.AlreadyExists", $"Ya existe otra sucursal registrada con el nombre '{request.Name}'."));
        }

        try
        {
            var address = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
            branch.UpdateDetails(request.Name, address, request.PhoneNumber);
        }
        catch (DomainException ex)
        {
            return Result.Fail<BranchDto>(DomainError.Validation("Branch.Invalid", ex.Message));
        }

        _branchRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(BranchDto.FromEntity(branch));
    }
}
