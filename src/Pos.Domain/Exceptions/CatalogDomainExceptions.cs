namespace Pos.Domain.Exceptions;

public class InvalidMoneyException : DomainException
{
    public InvalidMoneyException(string message) : base(message)
    {
    }
}

public class InvalidSkuException : DomainException
{
    public InvalidSkuException(string message) : base(message)
    {
    }
}

public class ProductNotFoundException : DomainException
{
    public ProductNotFoundException(Guid productId) : base($"El producto con ID '{productId}' no fue encontrado.")
    {
    }
}

public class CategoryNotFoundException : DomainException
{
    public CategoryNotFoundException(Guid categoryId) : base($"La categoría con ID '{categoryId}' no fue encontrada.")
    {
    }
}
