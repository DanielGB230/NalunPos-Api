using Microsoft.Extensions.Options;
using Pos.Api.Options;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.BackgroundServices;

/// <summary>
/// BackgroundService periódico que purga refresh tokens fuera de la ventana de
/// retención (F1.G.6 / ADR 0033). Sigue el patrón de
/// <see cref="OutboxProcessorBackgroundService"/>: PeriodicTimer, un scope por
/// ejecución, y try/catch que registra y continúa. Es idempotente y seguro con
/// varias instancias porque el borrado por lotes no depende de estado en memoria.
/// </summary>
public class RefreshTokenPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenPurgeBackgroundService> _logger;
    private readonly RefreshTokenPurgeSettings _options;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RefreshTokenPurgeBackgroundService> logger,
        IOptions<RefreshTokenPurgeSettings> options,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "RefreshTokenPurgeBackgroundService deshabilitado (RefreshTokenPurge:Enabled=false). No se programa ninguna purga.");
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "RefreshTokenPurgeBackgroundService iniciado. Intervalo: {IntervalMinutes} min, retención: {RetentionDays} días, lote: {BatchSize}.",
                _options.IntervalMinutes, _options.RetentionDays, _options.BatchSize);
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado al purgar refresh tokens.");
            }
        }
    }

    internal async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var purger = scope.ServiceProvider.GetRequiredService<IRefreshTokenPurger>();

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        int deleted = await purger.PurgeAsync(
            nowUtc,
            _options.Retention,
            _options.BatchSize,
            cancellationToken);

        if (deleted > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "RefreshTokenPurge: {Deleted} tokens eliminados (retención {RetentionDays} días).",
                deleted, _options.RetentionDays);
        }

        return deleted;
    }
}
