using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Notifications.Commands;
using Pos.Application.Notifications.DTOs;
using Pos.Application.Notifications.Queries;

namespace Pos.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public NotificationsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemNotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SystemNotificationDto>>> GetUserNotifications(
        Guid userId,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserNotificationsQuery(userId, unreadOnly);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SystemNotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SystemNotificationDto>> CreateNotification(
        [FromBody] CreateSystemNotificationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetUserNotifications), new { userId = result.UserId }, result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(SystemNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemNotificationDto>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var command = new MarkNotificationAsReadCommand(id);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return Ok(result);
    }
}
