using FluentValidation;
using FluentValidation.Results;
using Pos.Application.Common.Behaviors;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Validation;
using Pos.Domain.Common;
using Xunit;

namespace Pos.Application.Tests.Common.Validation;

public class ValidationResultFactoryTests
{
    [Fact]
    public void CreateError_GroupsFailuresByPropertyName_WithOrdinalComparison()
    {
        var failures = new[]
        {
            new ValidationFailure("Email", "Email es requerido"),
            new ValidationFailure("Nombre", "Nombre es requerido")
        };

        var error = ValidationResultFactory.CreateError(failures);

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("Validation.Error", error.Code);
        Assert.NotNull(error.Errors);
        Assert.True(error.Errors.Values.ContainsKey("Email"));
        Assert.True(error.Errors.Values.ContainsKey("Nombre"));
        Assert.Equal("Email es requerido", error.Errors.Values["Email"][0]);
        Assert.Equal("Nombre es requerido", error.Errors.Values["Nombre"][0]);
    }

    [Fact]
    public void CreateError_MultipleFailuresSameProperty_PreservesMessageOrder()
    {
        var failures = new[]
        {
            new ValidationFailure("Email", "Email es requerido"),
            new ValidationFailure("Email", "Email debe ser un correo válido"),
            new ValidationFailure("Email", "Email no debe exceder 100 caracteres")
        };

        var error = ValidationResultFactory.CreateError(failures);

        Assert.NotNull(error.Errors);
        Assert.Equal(3, error.Errors.Values["Email"].Count);
        Assert.Equal("Email es requerido", error.Errors.Values["Email"][0]);
        Assert.Equal("Email debe ser un correo válido", error.Errors.Values["Email"][1]);
        Assert.Equal("Email no debe exceder 100 caracteres", error.Errors.Values["Email"][2]);
    }

    [Fact]
    public void CreateResultForResponse_WithGenericResult_ReturnsGenericResultWithFieldErrors()
    {
        var failures = new[]
        {
            new ValidationFailure("Stock", "Stock insuficiente")
        };

        var result = ValidationResultFactory.CreateResultForResponse<Result<string>>(failures);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Error", result.Error.Code);
        Assert.NotNull(result.Error.Errors);
        Assert.Equal("Stock insuficiente", result.Error.Errors.Values["Stock"][0]);
    }

    [Fact]
    public void CreateResultForResponse_WithNonGenericResult_ReturnsNonGenericResultWithFieldErrors()
    {
        var failures = new[]
        {
            new ValidationFailure("Codigo", "Código es requerido")
        };

        var result = ValidationResultFactory.CreateResultForResponse<Result>(failures);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Error", result.Error.Code);
        Assert.NotNull(result.Error.Errors);
        Assert.Equal("Código es requerido", result.Error.Errors.Values["Codigo"][0]);
    }

    [Fact]
    public void CreateResultForResponse_UnsupportedType_ThrowsInvalidOperationException()
    {
        var failures = new[] { new ValidationFailure("Campo", "Error de prueba") };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidationResultFactory.CreateResultForResponse<Guid>(failures));

        Assert.Contains("Guid", ex.Message);
        Assert.Contains("no es un Result ni Result<T>", ex.Message);
    }

    [Fact]
    public void IsResultResponse_IdentifiesResultAndGenericResultTypesCorrectly()
    {
        Assert.True(ValidationResultFactory.IsResultResponse<Result>());
        Assert.True(ValidationResultFactory.IsResultResponse<Result<string>>());
        Assert.True(ValidationResultFactory.IsResultResponse<Result<int>>());
        Assert.False(ValidationResultFactory.IsResultResponse<Guid>());
        Assert.False(ValidationResultFactory.IsResultResponse<string>());
    }

    [Fact]
    public async Task ValidationDecorator_ResultResponse_ReturnsFailedResultWithFieldErrors()
    {
        var command = new DummyCommand();
        var innerHandler = new DummyCommandHandler();
        var validator = new DummyCommandValidator();

        var decorator = new ValidationDecorator<DummyCommand, Result<string>>(innerHandler, new[] { validator });

        var result = await decorator.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Error", result.Error.Code);
        Assert.NotNull(result.Error.Errors);
        Assert.Equal("Nombre requerido", result.Error.Errors.Values["Nombre"][0]);
    }

    [Fact]
    public async Task ValidationDecorator_NonResultResponse_ThrowsValidationException()
    {
        var command = new NonResultCommand();
        var innerHandler = new NonResultCommandHandler();
        var validator = new NonResultCommandValidator();

        var decorator = new ValidationDecorator<NonResultCommand, Guid>(innerHandler, new[] { validator });

        await Assert.ThrowsAsync<ValidationException>(() => decorator.HandleAsync(command));
    }

    // ── Clases auxiliares para probar ValidationDecorator ────────────────────

    public record DummyCommand : ICommand<Result<string>>;

    public class DummyCommandHandler : ICommandHandler<DummyCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(DummyCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok("Éxito"));
    }

    public class DummyCommandValidator : AbstractValidator<DummyCommand>
    {
        public DummyCommandValidator()
        {
            RuleFor(x => (object)x).Custom((_, ctx) =>
            {
                ctx.AddFailure("Nombre", "Nombre requerido");
            });
        }
    }

    public record NonResultCommand : ICommand<Guid>;

    public class NonResultCommandHandler : ICommandHandler<NonResultCommand, Guid>
    {
        public Task<Guid> HandleAsync(NonResultCommand command, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
    }

    public class NonResultCommandValidator : AbstractValidator<NonResultCommand>
    {
        public NonResultCommandValidator()
        {
            RuleFor(x => (object)x).Custom((_, ctx) =>
            {
                ctx.AddFailure("Id", "Id inválido");
            });
        }
    }
}
