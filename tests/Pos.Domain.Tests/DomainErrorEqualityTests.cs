using System.Collections.Frozen;
using Pos.Domain.Common;
using Xunit;

namespace Pos.Domain.Tests;

public class DomainErrorEqualityTests
{
    // ── FieldErrors: igualdad por valor independiente del orden de claves ─────

    [Fact]
    public void FieldErrors_SameKeysDifferentOrder_ShouldBeEqualAndHaveSameHashCode()
    {
        var dict1 = new Dictionary<string, string[]>
        {
            ["Email"]  = ["Email es requerido", "Email inválido"],
            ["Nombre"] = ["Nombre es requerido"]
        };

        var dict2 = new Dictionary<string, string[]>
        {
            ["Nombre"] = ["Nombre es requerido"],
            ["Email"]  = ["Email es requerido", "Email inválido"]
        };

        var fe1 = new FieldErrors(dict1);
        var fe2 = new FieldErrors(dict2);

        Assert.Equal(fe1, fe2);
        Assert.True(fe1 == fe2);
        Assert.Equal(fe1.GetHashCode(), fe2.GetHashCode());
    }

    [Fact]
    public void FieldErrors_DifferentValues_ShouldNotBeEqual()
    {
        var fe1 = new FieldErrors(new Dictionary<string, string[]> { ["Email"] = ["Email es requerido"] });
        var fe2 = new FieldErrors(new Dictionary<string, string[]> { ["Email"] = ["Email inválido"] });

        Assert.NotEqual(fe1, fe2);
        Assert.False(fe1 == fe2);
    }

    // ── FieldErrors: copias defensivas — mutar el diccionario/array de entrada no afecta ─────

    [Fact]
    public void FieldErrors_DefensiveCopy_OriginalArrayMutationDoesNotChangeInternalState()
    {
        var originalArray = new[] { "Error original" };
        var dict = new Dictionary<string, string[]> { ["Campo"] = originalArray };

        var fe = new FieldErrors(dict);
        var hashBefore = fe.GetHashCode();

        originalArray[0] = "Error mutado";                          // mutar el array de origen

        Assert.Equal("Error original", fe.Values["Campo"][0]);      // interno no cambió
        Assert.Equal(hashBefore, fe.GetHashCode());                  // hash consistente
    }

    // ── FieldErrors: inmutabilidad real — ningún cast de Values permite mutar ─────

    [Fact]
    public void FieldErrors_Values_CannotBeCastToMutableDictionary()
    {
        var fe = new FieldErrors(new Dictionary<string, string[]>
        {
            ["A"] = ["v1"]
        });

        // Tampoco se puede castear a Dictionary<K,V> concreto.
        var asConcreteDict = fe.Values as Dictionary<string, IReadOnlyList<string>>;
        Assert.Null(asConcreteDict);

        var extraArray = new[] { "v2" };

        // Al castear a IDictionary genérico, la referencia no es nula, IsReadOnly es true y cualquier mutación lanza NotSupportedException
        var asMutableGeneric = fe.Values as IDictionary<string, IReadOnlyList<string>>;
        Assert.NotNull(asMutableGeneric);
        Assert.True(asMutableGeneric.IsReadOnly, "FrozenDictionary debe reportarse como IsReadOnly=true");
        Assert.Throws<NotSupportedException>(() => asMutableGeneric.Add("B", extraArray));
        Assert.Throws<NotSupportedException>(() => asMutableGeneric.Remove("A"));
        Assert.Throws<NotSupportedException>(() => asMutableGeneric["A"] = extraArray);
        Assert.Throws<NotSupportedException>(() => asMutableGeneric.Clear());

        // Al castear a IDictionary no genérico
        var asNonGeneric = fe.Values as System.Collections.IDictionary;
        Assert.NotNull(asNonGeneric);
        Assert.True(asNonGeneric.IsReadOnly, "FrozenDictionary debe reportarse como IsReadOnly=true");
        Assert.Throws<NotSupportedException>(() => asNonGeneric.Add("B", extraArray));
        Assert.Throws<NotSupportedException>(() => asNonGeneric.Remove("A"));
        Assert.Throws<NotSupportedException>(() => asNonGeneric.Clear());

        // Intentar castear la lista devuelta a array mutable string[] lanza InvalidCastException
        Assert.Throws<InvalidCastException>(() => (string[])fe.Values["A"]);
    }

    [Fact]
    public void FieldErrors_ValuesEntry_IsImmutableArrayAndCannotBeCastToMutableArray()
    {
        var fe = new FieldErrors(new Dictionary<string, string[]> { ["X"] = ["a"] });
        var hashBefore = fe.GetHashCode();

        // No se puede castear a string[]
        Assert.Throws<InvalidCastException>(() => (string[])fe.Values["X"]);

        // El array de entrada tampoco afecta el estado interno si se muta después de la construcción
        var inputArray = new[] { "msg" };
        var fe2 = new FieldErrors(new Dictionary<string, string[]> { ["Y"] = inputArray });
        inputArray[0] = "mutado";

        Assert.Equal("msg", fe2.Values["Y"][0]);
        Assert.Equal(hashBefore, fe.GetHashCode());
    }

    // ── FieldErrors: entrada con null en el array de mensajes ─────

    [Fact]
    public void FieldErrors_WithNullValueInArray_TreatedAsEmptyArray()
    {
        // IDictionary<string, string[]> con valor null para un campo
        var dict = new Dictionary<string, string[]>
        {
            ["Campo"] = null!
        };

        // No debe lanzar
        var fe = new FieldErrors(dict);

        Assert.True(fe.Values.ContainsKey("Campo"));
        Assert.Empty(fe.Values["Campo"]);    // null tratado como []
    }

    // ── DomainError: Validation con FieldErrors ─────

    [Fact]
    public void DomainError_ValidationWithFieldErrors_SetsErrorsAndSupportsEquality()
    {
        var fe1 = new FieldErrors(new Dictionary<string, string[]>
        {
            ["Code"]  = ["Código requerido"],
            ["Price"] = ["Precio debe ser mayor a 0"]
        });

        var fe2 = new FieldErrors(new Dictionary<string, string[]>
        {
            ["Price"] = ["Precio debe ser mayor a 0"],
            ["Code"]  = ["Código requerido"]
        });

        var err1 = DomainError.Validation("VAL_001", "Errores de validación", fe1);
        var err2 = DomainError.Validation("VAL_001", "Errores de validación", fe2);

        Assert.NotNull(err1.Errors);
        Assert.Equal(ErrorType.Validation, err1.Type);
        Assert.Equal(fe1, err1.Errors);
        Assert.Equal(err1, err2);
        Assert.Equal(err1.GetHashCode(), err2.GetHashCode());
    }

    [Fact]
    public void DomainError_ValidationWithoutFieldErrors_ErrorsIsNull()
    {
        var err = DomainError.Validation("VAL_001", "Error simple");
        Assert.Null(err.Errors);
    }
}
