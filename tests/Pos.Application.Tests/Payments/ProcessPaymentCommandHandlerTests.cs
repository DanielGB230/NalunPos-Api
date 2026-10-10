using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.Commands;
using Pos.Application.Payments.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Payments;

public class ProcessPaymentCommandHandlerTests
{
    private readonly FakePaymentRepository _paymentRepository = new();
    private readonly FakeSaleRepository _saleRepository = new();
    private readonly FakePaymentGateway _paymentGateway = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDispatcher _dispatcher = new();
    private readonly ProcessPaymentCommandHandler _handler;

    public ProcessPaymentCommandHandlerTests()
    {
        _handler = new ProcessPaymentCommandHandler(
            _paymentRepository,
            _saleRepository,
            _paymentGateway,
            _unitOfWork,
            _dispatcher);
    }

    [Fact]
    public async Task HandleAsync_WithValidSaleAndSuccessfulGateway_ShouldProcessPaymentAndReturnSuccess()
    {
        // Arrange
        var lineItem = SaleLineItem.Create(Guid.NewGuid(), "Producto Test", 1m, Money.Create(50m, "USD"));
        var sale = Sale.Create("V-001-0001", Guid.NewGuid(), null, new[] { lineItem }, 0m, "USD");
        _saleRepository.Sales.Add(sale);
        _paymentGateway.ShouldSucceed = true;

        var command = new ProcessPaymentCommand(
            sale.Id,
            Amount: 50m,
            Method: PaymentMethod.CreditCard,
            Currency: "USD",
            ExternalReference: "REF-12345");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentStatus.Processed.ToString(), result.Value.StatusName);
        Assert.Single(_paymentRepository.Payments);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentSale_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new ProcessPaymentCommand(
            SaleId: Guid.NewGuid(),
            Amount: 50m,
            Method: PaymentMethod.Cash);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Sale.NotFound", result.Error.Code);
        Assert.Empty(_paymentRepository.Payments);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba

}
