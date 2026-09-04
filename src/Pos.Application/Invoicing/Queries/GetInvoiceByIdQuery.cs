using Pos.Application.Common.Interfaces;
using Pos.Application.Invoicing.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Invoicing.Queries;

public record GetInvoiceByIdQuery(Guid Id) : IQuery<InvoiceDto>;

public class GetInvoiceByIdQueryHandler : IQueryHandler<GetInvoiceByIdQuery, InvoiceDto>
{
    private readonly IInvoiceRepository _invoiceRepository;

    public GetInvoiceByIdQueryHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository ?? throw new ArgumentNullException(nameof(invoiceRepository));
    }

    public async Task<InvoiceDto> HandleAsync(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvoiceNotFoundException(request.Id);

        return InvoiceDto.FromEntity(invoice);
    }
}
