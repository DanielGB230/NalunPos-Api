namespace Pos.Domain.Exceptions;

public class BranchNotFoundException : DomainException
{
    public BranchNotFoundException(Guid branchId)
        : base($"La sucursal con ID '{branchId}' no fue encontrada.")
    {
    }
}

public class PosDeviceNotFoundException : DomainException
{
    public PosDeviceNotFoundException(Guid deviceId)
        : base($"El dispositivo POS con ID '{deviceId}' no fue encontrado.")
    {
    }
}

public class SystemNotificationNotFoundException : DomainException
{
    public SystemNotificationNotFoundException(Guid notificationId)
        : base($"La notificación con ID '{notificationId}' no fue encontrada.")
    {
    }
}
