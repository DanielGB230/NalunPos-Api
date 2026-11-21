namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto de purga de refresh tokens antiguos (F1.G.6).
/// Elimina únicamente tokens fuera de la ventana de retención; la retención
/// sostiene la detección de reuso de la familia.
/// </summary>
public interface IRefreshTokenPurger
{
    /// <summary>
    /// Elimina por lotes los tokens revocados o expirados antes de
    /// <paramref name="nowUtc"/> menos <paramref name="retention"/>.
    /// Devuelve el total de filas eliminadas.
    /// </summary>
    Task<int> PurgeAsync(
        DateTime nowUtc,
        TimeSpan retention,
        int batchSize,
        CancellationToken cancellationToken = default);
}
