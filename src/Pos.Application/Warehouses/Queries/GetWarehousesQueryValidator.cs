using FluentValidation;

namespace Pos.Application.Warehouses.Queries;

public class GetWarehousesQueryValidator : AbstractValidator<GetWarehousesQuery>
{
    public GetWarehousesQueryValidator()
    {
    }
}
