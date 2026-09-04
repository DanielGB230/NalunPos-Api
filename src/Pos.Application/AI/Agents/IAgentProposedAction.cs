using Pos.Domain.Enums;

namespace Pos.Application.AI.Agents;

/// <summary>
/// Contrato para propuestas de acciones estructuradas emitidas por Agentes Autónomos de IA.
/// </summary>
public interface IAgentProposedAction
{
    string ActionType { get; }
    string PayloadJson { get; }
    RiskLevel RiskLevel { get; }
}
