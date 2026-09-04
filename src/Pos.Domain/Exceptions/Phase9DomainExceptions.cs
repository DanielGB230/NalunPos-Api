namespace Pos.Domain.Exceptions;

public class AgentActionRecordNotFoundException : DomainException
{
    public AgentActionRecordNotFoundException(Guid recordId)
        : base($"El registro de propuesta de agente IA con ID '{recordId}' no fue encontrado.")
    {
    }
}

public class InvalidAgentActionStateException : DomainException
{
    public InvalidAgentActionStateException(string message)
        : base(message)
    {
    }
}
