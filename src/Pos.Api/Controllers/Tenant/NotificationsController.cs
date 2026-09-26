using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Notifications.Commands;
using Pos.Application.Notifications.DTOs;
using Pos.Application.Notifications.Queries;

namespace Pos.Api.Controllers.Tenant;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[EnableRateLimiting("GlobalApiPolicy")]
public class NotificationsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public NotificationsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(PagedResult<SystemNotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SystemNotificationDto>>> GetUserNotifications(
        Guid userId,
        [FromQuery] GetUserNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUserNotificationsQuery(
            userId,
            request.UnreadOnly ?? false,
            request.PageNumber,
            request.PageSize);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SystemNotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNotification(
        [FromBody] CreateSystemNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSystemNotificationCommand(request.UserId, request.Title, request.Message);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(SystemNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var command = new MarkNotificationAsReadCommand(id);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
