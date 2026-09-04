namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato agnóstico de la Capa Anti-Corrupción (ACL) para notificaciones push en tiempo real (SignalR, FCM, WebSockets).
/// </summary>
public interface IPushNotificationService
{
    Task SendToUserAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default);
}
