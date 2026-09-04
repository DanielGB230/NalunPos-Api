using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Branches.Commands;

public record CreateBranchCommand(
    string Name,
    string Street,
    string City,
    string ZipCode,
    string Country,
    string PhoneNumber = ""
) : ICommand<BranchDto>;

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

public class CreateBranchCommandHandler : ICommandHandler<CreateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBranchCommandHandler(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BranchDto> HandleAsync(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        bool exists = await _branchRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (exists)
        {
            throw new DomainException($"Ya existe una sucursal registrada con el nombre '{request.Name}'.");
        }

        var address = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
        var branch = Branch.Create(request.Name, address, request.PhoneNumber);

        await _branchRepository.AddAsync(branch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BranchDto.FromEntity(branch);
    }
}
