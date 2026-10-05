using System.Globalization;
using FluentValidation;
using Pos.Application.AI.Queries;
using Pos.Application.Branches.Queries;
using Pos.Application.CashRegisters.Queries;
using Pos.Application.Categories.Queries;
using Pos.Application.Common.Validation;
using Pos.Application.Customers.Queries;
using Pos.Application.Inventory.Queries;
using Pos.Application.Invoicing.Queries;
using Pos.Application.Payments.Queries;
using Pos.Application.PosDevices.Queries;
using Pos.Application.Products.Queries;
using Pos.Application.Sales.Queries;
using Pos.Application.Suppliers.Queries;
using Pos.Application.Users.Queries;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

public class IdQueryValidatorsTests
{
    public static IEnumerable<object[]> GetValidatorTestData()
    {
        var validId = Guid.NewGuid();

        return new List<object[]>
        {
            new object[] { new GetDemandForecastQueryValidator(), new GetDemandForecastQuery(Guid.Empty, 30), new GetDemandForecastQuery(validId, 30), "ProductId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetDemandForecastQueryValidator(), new GetDemandForecastQuery(validId, 0), new GetDemandForecastQuery(validId, 1), "DaysAhead", DemandForecastRules.DaysAheadErrorMessage },
            new object[] { new GetDemandForecastQueryValidator(), new GetDemandForecastQuery(validId, 366), new GetDemandForecastQuery(validId, 365), "DaysAhead", DemandForecastRules.DaysAheadErrorMessage },
            new object[] { new GetBranchByIdQueryValidator(), new GetBranchByIdQuery(Guid.Empty), new GetBranchByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetActiveSessionQueryValidator(), new GetActiveSessionQuery(Guid.Empty), new GetActiveSessionQuery(validId), "CashRegisterId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetCategoryByIdQueryValidator(), new GetCategoryByIdQuery(Guid.Empty), new GetCategoryByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetCustomerByIdQueryValidator(), new GetCustomerByIdQuery(Guid.Empty), new GetCustomerByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetProductStockQueryValidator(), new GetProductStockQuery(Guid.Empty, null), new GetProductStockQuery(validId, null), "ProductId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetProductStockQueryValidator(), new GetProductStockQuery(validId, Guid.Empty), new GetProductStockQuery(validId, validId), "WarehouseId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetInvoiceByIdQueryValidator(), new GetInvoiceByIdQuery(Guid.Empty), new GetInvoiceByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetPaymentsBySaleIdQueryValidator(), new GetPaymentsBySaleIdQuery(Guid.Empty), new GetPaymentsBySaleIdQuery(validId), "SaleId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetPosDeviceByIdQueryValidator(), new GetPosDeviceByIdQuery(Guid.Empty), new GetPosDeviceByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetPosDevicesByBranchQueryValidator(), new GetPosDevicesByBranchQuery(Guid.Empty), new GetPosDevicesByBranchQuery(validId), "BranchId", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetProductByIdQueryValidator(), new GetProductByIdQuery(Guid.Empty), new GetProductByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetSaleByIdQueryValidator(), new GetSaleByIdQuery(Guid.Empty), new GetSaleByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetSupplierByIdQueryValidator(), new GetSupplierByIdQuery(Guid.Empty), new GetSupplierByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
            new object[] { new GetUserByIdQueryValidator(), new GetUserByIdQuery(Guid.Empty), new GetUserByIdQuery(validId), "Id", IdValidationExtensions.RequiredIdErrorMessage },
        };
    }

    [Theory]
    [MemberData(nameof(GetValidatorTestData))]
    public void Validate_WhenQueryInvalid_ShouldFailForExpectedPropertyWithMessage(
        IValidator validator,
        object invalidQuery,
        object validQuery,
        string expectedPropertyName,
        string expectedErrorMessage)
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // Act - Invalid
            var contextInvalid = new ValidationContext<object>(invalidQuery);
            var resultInvalid = validator.Validate(contextInvalid);

            // Assert - Invalid
            Assert.False(resultInvalid.IsValid);
            var failure = Assert.Single(resultInvalid.Errors);
            Assert.Equal(expectedPropertyName, failure.PropertyName);
            Assert.Equal(expectedErrorMessage, failure.ErrorMessage);

            // Act - Valid
            var contextValid = new ValidationContext<object>(validQuery);
            var resultValid = validator.Validate(contextValid);

            // Assert - Valid
            Assert.True(resultValid.IsValid);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
