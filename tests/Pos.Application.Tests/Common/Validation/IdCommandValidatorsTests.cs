using FluentValidation;
using Pos.Application.Branches.Commands;
using Pos.Application.Categories.Commands;
using Pos.Application.Common.Validation;
using Pos.Application.Customers.Commands;
using Pos.Application.Invoicing.Commands;
using Pos.Application.Notifications.Commands;
using Pos.Application.PosDevices.Commands;
using Pos.Application.Products.Commands;
using Pos.Application.PurchaseOrders.Commands;
using Pos.Application.Suppliers.Commands;
using Pos.Application.Users.Commands;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

public class IdCommandValidatorsTests
{
    public static IEnumerable<object[]> GetValidatorTestData()
    {
        var validId = Guid.NewGuid();

        return new List<object[]>
        {
            new object[] { new ActivateBranchCommandValidator(), new ActivateBranchCommand(Guid.Empty), new ActivateBranchCommand(validId), "Id" },
            new object[] { new DeactivateBranchCommandValidator(), new DeactivateBranchCommand(Guid.Empty), new DeactivateBranchCommand(validId), "Id" },
            new object[] { new ActivateCategoryCommandValidator(), new ActivateCategoryCommand(Guid.Empty), new ActivateCategoryCommand(validId), "Id" },
            new object[] { new DeactivateCategoryCommandValidator(), new DeactivateCategoryCommand(Guid.Empty), new DeactivateCategoryCommand(validId), "Id" },
            new object[] { new ActivateCustomerCommandValidator(), new ActivateCustomerCommand(Guid.Empty), new ActivateCustomerCommand(validId), "Id" },
            new object[] { new DeactivateCustomerCommandValidator(), new DeactivateCustomerCommand(Guid.Empty), new DeactivateCustomerCommand(validId), "Id" },
            new object[] { new ActivateProductCommandValidator(), new ActivateProductCommand(Guid.Empty), new ActivateProductCommand(validId), "Id" },
            new object[] { new DeactivateProductCommandValidator(), new DeactivateProductCommand(Guid.Empty), new DeactivateProductCommand(validId), "Id" },
            new object[] { new ActivateSupplierCommandValidator(), new ActivateSupplierCommand(Guid.Empty), new ActivateSupplierCommand(validId), "Id" },
            new object[] { new DeactivateSupplierCommandValidator(), new DeactivateSupplierCommand(Guid.Empty), new DeactivateSupplierCommand(validId), "Id" },
            new object[] { new ActivateUserCommandValidator(), new ActivateUserCommand(Guid.Empty), new ActivateUserCommand(validId), "Id" },
            new object[] { new DeactivateUserCommandValidator(), new DeactivateUserCommand(Guid.Empty), new DeactivateUserCommand(validId), "Id" },
            new object[] { new MarkNotificationAsReadCommandValidator(), new MarkNotificationAsReadCommand(Guid.Empty), new MarkNotificationAsReadCommand(validId), "Id" },
            new object[] { new PingPosDeviceCommandValidator(), new PingPosDeviceCommand(Guid.Empty), new PingPosDeviceCommand(validId), "Id" },
            new object[] { new ReconcileInvoiceCommandValidator(), new ReconcileInvoiceCommand(Guid.Empty), new ReconcileInvoiceCommand(validId), "InvoiceId" },
            new object[] { new SendPurchaseOrderCommandValidator(), new SendPurchaseOrderCommand(Guid.Empty), new SendPurchaseOrderCommand(validId), "PurchaseOrderId" }
        };
    }

    [Theory]
    [MemberData(nameof(GetValidatorTestData))]
    public void Validate_WhenGuidIsEmpty_ShouldFailForExpectedPropertyWithMessage(
        IValidator validator,
        object invalidCommand,
        object validCommand,
        string expectedPropertyName)
    {
        // Act - Invalid
        var contextInvalid = new ValidationContext<object>(invalidCommand);
        var resultInvalid = validator.Validate(contextInvalid);

        // Assert - Invalid
        Assert.False(resultInvalid.IsValid);
        var failure = Assert.Single(resultInvalid.Errors);
        Assert.Equal(expectedPropertyName, failure.PropertyName);
        Assert.Equal(IdValidationExtensions.RequiredIdErrorMessage, failure.ErrorMessage);

        // Act - Valid
        var contextValid = new ValidationContext<object>(validCommand);
        var resultValid = validator.Validate(contextValid);

        // Assert - Valid
        Assert.True(resultValid.IsValid);
    }
}
