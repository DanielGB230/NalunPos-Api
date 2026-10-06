using System.Globalization;
using FluentValidation;
using Pos.Application.Common.Validation;
using Pos.Application.PurchaseOrders.Commands;
using Pos.Application.StockAdjustments.Commands;
using Pos.Application.StockTransfers.Commands;
using Pos.Application.Warehouses.Commands;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

[Collection("NonParallelValidationTests")]
public class DeterministicValidationMessagesTests
{
    public static IEnumerable<object[]> GetFiveValidatorsTestData()
    {
        return new List<object[]>
        {
            // 1. ReceivePurchaseOrderCommandValidator - ProductId in ChildRules
            new object[]
            {
                new ReceivePurchaseOrderCommandValidator(),
                new ReceivePurchaseOrderCommand(Guid.NewGuid(), [new ReceivePurchaseOrderLineDto(Guid.Empty, 10m)]),
                "ReceivedLines[0].ProductId",
                IdValidationExtensions.RequiredIdErrorMessage
            },
            // 2. CreatePurchaseOrderCommandValidator - OrderNumber.NotEmpty() when empty string
            new object[]
            {
                new CreatePurchaseOrderCommandValidator(),
                new CreatePurchaseOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "", [new CreatePurchaseOrderLineDto(Guid.NewGuid(), 1m, 10m)]),
                "OrderNumber",
                "El número de orden es requerido."
            },
            // 3. CreateStockAdjustmentCommandValidator - ProductId in ChildRules
            new object[]
            {
                new CreateStockAdjustmentCommandValidator(),
                new CreateStockAdjustmentCommand(Guid.NewGuid(), Pos.Domain.Enums.StockAdjustmentReason.Breakage, [new CreateStockAdjustmentLineDto(Guid.Empty, 1m)]),
                "Lines[0].ProductId",
                IdValidationExtensions.RequiredIdErrorMessage
            },
            // 4. CreateStockTransferCommandValidator - ProductId in ChildRules
            new object[]
            {
                new CreateStockTransferCommandValidator(),
                new CreateStockTransferCommand(Guid.NewGuid(), Guid.NewGuid(), [new CreateStockTransferLineDto(Guid.Empty, 1m)]),
                "Lines[0].ProductId",
                IdValidationExtensions.RequiredIdErrorMessage
            },
            // 5. CreateWarehouseCommandValidator - Name.NotEmpty() when empty string
            new object[]
            {
                new CreateWarehouseCommandValidator(),
                new CreateWarehouseCommand(Guid.NewGuid(), ""),
                "Name",
                "El nombre del almacén es requerido."
            }
        };
    }

    [Theory]
    [MemberData(nameof(GetFiveValidatorsTestData))]
    public void Validate_UnderEnUsCulture_ReturnsExplicitSpanishMessage(
        IValidator validator,
        object invalidCommand,
        string expectedPropertyName,
        string expectedErrorMessage)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var context = new ValidationContext<object>(invalidCommand);
            var result = validator.Validate(context);

            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal(expectedPropertyName, error.PropertyName);
            Assert.Equal(expectedErrorMessage, error.ErrorMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private sealed class TestCommand
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestCommandValidatorWithoutExplicitWithMessage : AbstractValidator<TestCommand>
    {
        public TestCommandValidatorWithoutExplicitWithMessage()
        {
            RuleFor(x => x.Name).NotEmpty(); // No explicit .WithMessage(...)
        }
    }

    [Fact]
    public void FluentValidationCultureConfiguration_WhenConfigured_ProducesSpanishMessageForRulesWithoutExplicitWithMessage()
    {
        var originalGlobalCulture = ValidatorOptions.Global.LanguageManager.Culture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // Apply global culture configuration
            FluentValidationCultureConfiguration.ConfigureDefaultCulture();

            var validator = new TestCommandValidatorWithoutExplicitWithMessage();
            var result = validator.Validate(new TestCommand { Name = string.Empty });

            Assert.False(result.IsValid);
            var error = Assert.Single(result.Errors);
            Assert.Equal("Name", error.PropertyName);
            Assert.Contains("no debería estar vacío", error.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ValidatorOptions.Global.LanguageManager.Culture = originalGlobalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
