using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Categories.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Categories.Commands;

[RequiresTenant]
[HasPermission(Permissions.Categories.Create)]
public record CreateCategoryCommand(string Name, string? Description) : ICommand<Result<CategoryDto>>;

public class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateCategoryCommandHandler(
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<CategoryDto>> HandleAsync(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        bool nameExists = await _categoryRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (nameExists)
        {
            return Result.Fail<CategoryDto>(DomainError.Conflict("Category.AlreadyExists", $"Ya existe una categoría con el nombre '{request.Name}'."));
        }

        Category category;
        try
        {
            category = Category.Create(_tenantContext.RequireTenantId(), request.Name, request.Description, _timeProvider);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CategoryDto>(DomainError.Validation("Category.Invalid", ex.Message));
        }

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CategoryDto.FromEntity(category));
    }
}
