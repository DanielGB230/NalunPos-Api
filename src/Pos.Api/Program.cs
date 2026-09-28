using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pos.Api.BackgroundServices;
using Pos.Api.Extensions;
using Pos.Api.Middleware;
using Pos.Application;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Seed;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Observabilidad (Logging JSON Estructurado y OpenTelemetry)
builder.Services.AddCustomObservability(builder.Logging);

// 2. Versionado de API
builder.Services.AddCustomApiVersioning();

// 3. Rate Limiting Nativo (.NET 10)
builder.Services.AddCustomRateLimiting(builder.Configuration);

// 4. Inyección de dependencias de capas Clean Architecture (Application + Infrastructure)
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

// 5. Registro de configuración para OutboxWorker e InvoiceReconciliation
builder.Services.Configure<OutboxSettings>(builder.Configuration.GetSection(OutboxSettings.SectionName));
builder.Services.Configure<InvoiceReconciliationSettings>(builder.Configuration.GetSection("InvoiceReconciliation"));

// 6. Registro de Background Workers
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<InvoiceReconciliationBackgroundService>();

// 7. Autenticación JWT y Autorización Fail-Closed
builder.Services.AddCustomAuthentication(builder.Configuration);

// 8. Configuración de CORS
builder.Services.AddCustomCors(builder.Configuration);

// 9. Configuración de Controladores
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 10. Configuración de OpenAPI y Scalar
builder.Services.AddCustomOpenApi();

var app = builder.Build();

// Inicialización de Base de Datos y Seeder
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<PosDbContext>();
        if (app.Environment.IsDevelopment() && dbContext.Database.IsSqlServer())
        {
            logger.LogInformation("Aplicando migraciones de base de datos pendientes...");
            await dbContext.Database.MigrateAsync();
        }

        var seeder = services.GetRequiredService<SuperAdminSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrió un error al ejecutar la siembra del SuperAdmin en el inicio.");
    }
}

// Pipeline de middlewares (orden crítico):
// 1. ForwardedHeaders: PRIMERO, para que la IP real esté disponible desde el inicio
app.UseCustomForwardedHeaders();

// 2. CorrelationId: envuelve a ExceptionHandling para que los errores se logueen con el CorrelationId ya establecido
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("Nalun POS API Documentation")
               .WithTheme(ScalarTheme.Purple);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program;
