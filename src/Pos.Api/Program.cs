using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Api.BackgroundServices;
using Pos.Api.Extensions;
using Pos.Api.Middleware;
using Pos.Api.ModelBinding;
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

builder.Services.AddOptions<Pos.Infrastructure.TenantStatusOptions>()
    .Bind(builder.Configuration.GetSection(Pos.Infrastructure.TenantStatusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<Pos.Infrastructure.AccessTokenLifetimeOptions>()
    .Bind(builder.Configuration.GetSection(Pos.Infrastructure.AccessTokenLifetimeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 5b. Registro de opciones de Refresh Token con ValidateOnStart
builder.Services.AddOptions<Pos.Application.Common.Options.RefreshTokenOptions>()
    .Bind(builder.Configuration.GetSection(Pos.Application.Common.Options.RefreshTokenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RefreshTokenPurgeSettings>()
    .Bind(builder.Configuration.GetSection(RefreshTokenPurgeSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Exponer el POCO de opciones como singleton para inyección directa en handlers de Application
builder.Services.AddSingleton<Pos.Application.Common.Options.RefreshTokenOptions>(
    sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Pos.Application.Common.Options.RefreshTokenOptions>>().Value);

// 6. Registro de Background Workers
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<InvoiceReconciliationBackgroundService>();
builder.Services.AddHostedService<RefreshTokenPurgeBackgroundService>();

// 7. Autenticación JWT y Autorización Fail-Closed
builder.Services.AddCustomAuthentication(builder.Configuration);

// 8. Configuración de CORS. Mismo patrón que OutboxSettings: Bind + Validate + ValidateOnStart.
//    - CorsSettings es un POCO simple (sin IValidatableObject ni data annotations).
//    - CorsOriginsRules contiene las reglas puras de validación de orígenes.
//    - CorsOriginsOptionsValidator añade el mensaje dinámico con el origen ofensor.
builder.Services.AddSingleton<IValidateOptions<CorsSettings>, CorsOriginsOptionsValidator>();
builder.Services.AddOptions<CorsSettings>()
    .Bind(builder.Configuration.GetSection(CorsSettings.SectionName))
    .Validate(settings => CorsOriginsRules.HasValidOrigins(settings.AllowedOrigins), CorsOriginsRules.ValidationFailureMessage)
    .ValidateOnStart();

builder.Services.AddCustomCors();

// 9. Configuración de Controladores y ProblemDetails
builder.Services.AddControllers(options =>
    {
        // Contrato de filtros de fecha en query string: instante UTC con offset explícito.
        options.ModelBinderProviders.Insert(0, new UtcInstantModelBinderProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddCustomProblemDetails();

// 10. Configuración de OpenAPI y Scalar
builder.Services.AddCustomOpenApi();

// 10b. Escritor de cookie de refresh token (F1.G)
builder.Services.AddScoped<RefreshTokenCookieWriter>();

// 11. Health Checks nativos (.NET 10 / H13)
builder.Services.AddCustomHealthChecks();

var app = builder.Build();

// Fail-fast determinista de la configuración CORS (ADR 0035): las opciones se resuelven y
// validan ANTES de cualquier acceso a la base de datos, para que un error de configuración
// no se enmascare con un fallo de infraestructura. ValidateOnStart mantiene el fail-fast
// del host en el resto de caminos de arranque.
_ = app.Services.GetRequiredService<IOptions<CorsSettings>>().Value;

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
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference("/scalar", options =>
    {
        options.WithTitle("Nalun POS API Documentation")
               .WithTheme(ScalarTheme.Purple);
    }).AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<TenantStatusEnforcementMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapCustomHealthChecks();
app.MapControllers();

app.Run();

public partial class Program;
