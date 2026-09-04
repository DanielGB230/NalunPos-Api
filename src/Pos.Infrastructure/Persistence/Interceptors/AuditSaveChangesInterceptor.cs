using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de EF Core que captura automáticamente todas las inserciones, modificaciones y eliminaciones
/// e inserta un AuditLog con los valores anteriores y posteriores serializados en JSON.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            AuditChanges(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            AuditChanges(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void AuditChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();

        var auditEntries = new List<AuditLog>();
        Guid? currentUserId = _currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Evitar auditar el propio registro de auditoría para evitar recursividad infinita
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            string tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
            string recordId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? Guid.NewGuid().ToString();

            AuditAction action = entry.State switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update
            };

            var oldDictionary = new Dictionary<string, object?>();
            var newDictionary = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                string propName = property.Metadata.Name;

                if (entry.State == EntityState.Added)
                {
                    newDictionary[propName] = property.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    oldDictionary[propName] = property.OriginalValue;
                }
                else if (entry.State == EntityState.Modified && property.IsModified)
                {
                    oldDictionary[propName] = property.OriginalValue;
                    newDictionary[propName] = property.CurrentValue;
                }
            }

            string? oldValuesJson = oldDictionary.Count > 0 ? JsonSerializer.Serialize(oldDictionary) : null;
            string? newValuesJson = newDictionary.Count > 0 ? JsonSerializer.Serialize(newDictionary) : null;

            var auditLog = AuditLog.Create(
                tableName,
                recordId,
                action,
                currentUserId,
                oldValuesJson,
                newValuesJson
            );

            auditEntries.Add(auditLog);
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
    }
}
