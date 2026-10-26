using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pos.Api.BackgroundServices;
using Pos.Api.Extensions;
using Pos.Api.Middleware;
using Pos.Api.Options;
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

// 5. Registro de configuración para OutboxWorker, InvoiceReconciliation y SuperAdminSettings con ValidateOnStart
builder.Services.AddOptions<OutboxSettings>()
    .Bind(builder.Configuration.GetSection(OutboxSettings.SectionName))
    .ValidateDataAnnotations()
    .Validate(settings => settings.BatchSize * settings.MessageTimeoutSeconds < settings.LeaseDurationSeconds,
        "Invariante violada: BatchSize * MessageTimeoutSeconds debe ser estrictamente menor que LeaseDurationSeconds para prevenir expiración prematura de leases.")
    .ValidateOnStart();

builder.Services.AddOptions<InvoiceReconciliationSettings>()
    .Bind(builder.Configuration.GetSection("InvoiceReconciliation"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SuperAdminSettings>()
    .Bind(builder.Configuration.GetSection(SuperAdminSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 6. Registro de Background Workers
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<InvoiceReconciliationBackgroundService>();

// 7. Autenticación JWT y Autorización Fail-Closed
builder.Services.AddCustomAuthentication(builder.Configuration);

// 8. Configuración de CORS
builder.Services.AddCustomCors(builder.Configuration);

// 9. Configuración de Controladores y ProblemDetails
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddCustomProblemDetails();

// 10. Configuración de OpenAPI y Scalar
builder.Services.AddCustomOpenApi();

// 11. Health Checks nativos (.NET 10 / H13)
builder.Services.AddCustomHealthChecks();

var app = builder.Build();

// Inicialización determinista y fail-fast de Base de Datos y Seeder
await app.InitializeDatabaseAsync();

// Pipeline de middlewares (orden crítico):
// 1. ForwardedHeaders: PRIMERO, para que la IP real esté disponible desde el inicio
app.UseCustomForwardedHeaders();

// 2. CorrelationId y ExceptionHandler nativo (.NET 10 / RFC 9457)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("Nalun POS API Documentation")
               .WithTheme(ScalarTheme.Purple);
    });
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapCustomHealthChecks();
app.MapControllers();

app.Run();

public partial class Program;
