using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Notifications.Dummy;

/// <summary>
/// Implementación simulada de la Capa Anti-Corrupción (ACL) para servicios de notificación Push (SignalR/FCM).
/// Registra la notificación enviada en los logs nativos sin acoplar la aplicación a proveedores específicos.
/// </summary>
public class DummyPushNotificationService : IPushNotificationService
{
    private readonly ILogger<DummyPushNotificationService> _logger;

    public DummyPushNotificationService(ILogger<DummyPushNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SendToUserAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[ACL Push Notification] Notificación enviada exitosamente al usuario {UserId} | Título: {Title} | Mensaje: {Message}",
                userId,
                title,
                message);
        }

        return Task.CompletedTask;
    }
}
