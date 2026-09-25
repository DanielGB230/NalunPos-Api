using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Invoicing.Queries;

[HasPermission(Permissions.Invoices.View)]
public record GetInvoiceByIdQuery(Guid Id) : IQuery<Result<InvoiceDto>>;

[HasPermission(Permissions.Invoices.View)]
public class GetInvoiceByIdQueryHandler : IQueryHandler<GetInvoiceByIdQuery, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetInvoiceByIdQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
    }

    public async Task<Result<InvoiceDto>> HandleAsync(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (invoice == null)
        {
            return Result.Fail<InvoiceDto>(DomainError.NotFound("Invoice.NotFound", $"No se encontró la factura con el ID '{request.Id}'."));
        }

        return Result.Ok(InvoiceDto.FromEntity(invoice));
    }
}
