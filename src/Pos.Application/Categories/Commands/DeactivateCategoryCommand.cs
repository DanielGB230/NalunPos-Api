using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Commands;

[HasPermission(Permissions.Categories.Update)]
public record DeactivateCategoryCommand(Guid Id) : ICommand<Result<bool>>;

[HasPermission(Permissions.Categories.Update)]
public class DeactivateCategoryCommandHandler : ICommandHandler<DeactivateCategoryCommand, Result<bool>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(DeactivateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Category.NotFound", $"No se encontró la categoría con el ID '{request.Id}'."));
        }

        if (!category.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Category.AlreadyInactive", $"La categoría con ID '{request.Id}' ya se encuentra inactiva."));
        }

        category.Deactivate();
        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
