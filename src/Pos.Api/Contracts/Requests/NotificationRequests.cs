namespace Pos.Api.Contracts.Requests;

public record GetUserNotificationsRequest : PaginationRequest
{
    public bool? UnreadOnly { get; init; }
}

public record CreateSystemNotificationRequest(
    Guid UserId,
    string Title,
    string Message
);
