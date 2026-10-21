using FluentValidation;

namespace Pos.Application.CashRegisters.Queries;

public class GetCashRegistersQueryValidator : AbstractValidator<GetCashRegistersQuery>
{
    public GetCashRegistersQueryValidator()
    {
    }
}
