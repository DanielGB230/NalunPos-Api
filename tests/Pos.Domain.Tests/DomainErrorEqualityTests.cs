using Pos.Domain.Common;
using Xunit;

namespace Pos.Domain.Tests;

public class DomainErrorEqualityTests
{
    [Fact]
    public void FieldErrors_SameKeysDifferentOrder_ShouldBeEqualAndHaveSameHashCode()
    {
        var dict1 = new Dictionary<string, string[]>
        {
            ["Email"] = ["Email es requerido", "Email inválido"],
            ["Nombre"] = ["Nombre es requerido"]
        };

        var dict2 = new Dictionary<string, string[]>
        {
            ["Nombre"] = ["Nombre es requerido"],
            ["Email"] = ["Email es requerido", "Email inválido"]
        };

        var fieldErrors1 = new FieldErrors(dict1);
        var fieldErrors2 = new FieldErrors(dict2);

        Assert.Equal(fieldErrors1, fieldErrors2);
        Assert.True(fieldErrors1 == fieldErrors2);
        Assert.Equal(fieldErrors1.GetHashCode(), fieldErrors2.GetHashCode());
    }

    [Fact]
    public void FieldErrors_DifferentValues_ShouldNotBeEqual()
    {
        var dict1 = new Dictionary<string, string[]>
        {
            ["Email"] = ["Email es requerido"]
        };

        var dict2 = new Dictionary<string, string[]>
        {
            ["Email"] = ["Email inválido"]
        };

        var fieldErrors1 = new FieldErrors(dict1);
        var fieldErrors2 = new FieldErrors(dict2);

        Assert.NotEqual(fieldErrors1, fieldErrors2);
        Assert.False(fieldErrors1 == fieldErrors2);
    }

    [Fact]
    public void FieldErrors_DefensiveCopy_PreventsExternalArrayMutation()
    {
        var originalArray = new[] { "Error original" };
        var dict = new Dictionary<string, string[]>
        {
            ["Campo"] = originalArray
        };

        var fieldErrors = new FieldErrors(dict);

        originalArray[0] = "Error mutado";

        Assert.Equal("Error original", fieldErrors.Values["Campo"][0]);
    }

    [Fact]
    public void DomainError_ValidationWithFieldErrors_SetsErrorsAndSupportsEquality()
    {
        var dict1 = new Dictionary<string, string[]>
        {
            ["Code"] = ["Código requerido"],
            ["Price"] = ["Precio debe ser mayor a 0"]
        };

        var dict2 = new Dictionary<string, string[]>
        {
            ["Price"] = ["Precio debe ser mayor a 0"],
            ["Code"] = ["Código requerido"]
        };

        var fe1 = new FieldErrors(dict1);
        var fe2 = new FieldErrors(dict2);

        var err1 = DomainError.Validation("VAL_001", "Errores de validación", fe1);
        var err2 = DomainError.Validation("VAL_001", "Errores de validación", fe2);

        Assert.NotNull(err1.Errors);
        Assert.Equal(ErrorType.Validation, err1.Type);
        Assert.Equal(fe1, err1.Errors);
        Assert.Equal(err1, err2);
        Assert.Equal(err1.GetHashCode(), err2.GetHashCode());
    }
}
