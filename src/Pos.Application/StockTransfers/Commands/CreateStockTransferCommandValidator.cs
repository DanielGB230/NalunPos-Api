using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockTransfers.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Application.Common.Validation;

namespace Pos.Application.StockTransfers.Commands;

public class CreateStockTransferCommandValidator : AbstractValidator<CreateStockTransferCommand>
{
    public CreateStockTransferCommandValidator()
    {
        RuleFor(x => x.SourceWarehouseId).NotEmpty().WithMessage("El ID del almacén de origen es requerido.");
        RuleFor(x => x.DestinationWarehouseId).NotEmpty().WithMessage("El ID del almacén de destino es requerido.");
        RuleFor(x => x)
            .Must(x => x.SourceWarehouseId != x.DestinationWarehouseId)
            .WithMessage("El almacén de origen y destino no pueden ser el mismo.");

        RuleFor(x => x.Lines).NotEmpty().WithMessage("El traspaso debe contener al menos una línea de producto.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.ProductId).NotEmpty().WithMessage(IdValidationExtensions.RequiredIdErrorMessage);
            l.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad a traspasar debe ser mayor a cero.");
        });
    }
}
