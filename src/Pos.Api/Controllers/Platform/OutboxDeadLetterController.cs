using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Contracts.Responses;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Outbox.Commands;
using Pos.Application.Platform.Outbox.Queries;

namespace Pos.Api.Controllers.Platform;

/// <summary>
/// Controlador de plataforma para inspeccionar y reactivar mensajes Dead-Letter del Outbox.
/// Exclusivo para usuarios con rol SuperAdmin.
/// Ultra-delgado: Delega el 100% de la ejecución a IDispatcher.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/platform/outbox/dead-letter")]
[Authorize(Roles = "SuperAdmin")]
[EnableRateLimiting("SensitiveOperationsPolicy")]
public class OutboxDeadLetterController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OutboxDeadLetterController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Consulta los mensajes del Outbox transaccional que se encuentran en estado Dead-Letter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeadLetterMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DeadLetterMessageResponse>>> GetDeadLetterMessages(
        [FromQuery] GetDeadLetterMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var take = request.PageSize ?? request.Take ?? 50;
        var query = new GetDeadLetterMessagesQuery(take);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result, messages => messages.Select(m => new DeadLetterMessageResponse(
            m.Id,
            m.TenantId,
            m.Type,
            m.Content,
            m.OccurredOnUtc,
            m.RetryCount,
            m.Error)).ToList() as IReadOnlyList<DeadLetterMessageResponse>);
    }

    /// <summary>
    /// Reactiva una lista de mensajes en Dead-Letter para su reprocesamiento en el Outbox transaccional.
    /// </summary>
    [HttpPost("reprocess")]
    [ProducesResponseType(typeof(ReprocessDeadLetterMessagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReprocessDeadLetterMessagesResponse>> ReprocessDeadLetterMessages(
        [FromBody] ReprocessDeadLetterMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ReprocessDeadLetterMessagesCommand(request?.MessageIds ?? Array.Empty<Guid>());

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result, count => new ReprocessDeadLetterMessagesResponse(count));
    }
}
