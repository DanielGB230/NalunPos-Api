using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry.Trace;
using Pos.Api.BackgroundServices;
using Pos.Api.Middleware;
using Pos.Application;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence.Context;
using Pos.Infrastructure.Persistence.Seed;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Logging Estructurado con JsonConsole e IncludeScopes = true
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
    {
        Indented = false
    };
});

// 2. Configuración de OpenTelemetry Tracing Básica con Console Exporter
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Pos.Api")
        .AddAspNetCoreInstrumentation(options => options.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// 3. Configuración de Asp.Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// 4. Configuración de Rate Limiting nativo (.NET 10)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Demasiadas peticiones",
            Detail = "Se ha superado el límite de peticiones permitido. Intente nuevamente en unos momentos.",
            Instance = context.HttpContext.Request.Path
        };
        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    };

    // AuthPolicy: 10/min por IP para login/refresh
    options.AddPolicy("AuthPolicy", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // PosCheckoutPolicy: 120/min por TenantId para CreateSale
    options.AddPolicy("PosCheckoutPolicy", httpContext =>
    {
        var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: tenantId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0
            });
    });

    // SensitiveOperationsPolicy: 60/min por TenantId para CreateUser, IssueInvoice, etc.
    options.AddPolicy("SensitiveOperationsPolicy", httpContext =>
    {
        var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: tenantId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // GlobalApiPolicy: 300/min por TenantId para el resto
    options.AddPolicy("GlobalApiPolicy", httpContext =>
    {
        var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: tenantId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// Inyección de dependencias de capas Clean Architecture (Application + Infrastructure)
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

// Registro de configuración para OutboxWorker e InvoiceReconciliation
builder.Services.Configure<OutboxSettings>(builder.Configuration.GetSection(OutboxSettings.SectionName));
builder.Services.Configure<InvoiceReconciliationSettings>(builder.Configuration.GetSection("InvoiceReconciliation"));

// Registro de Background Workers
builder.Services.AddHostedService<OutboxProcessorBackgroundService>();
builder.Services.AddHostedService<InvoiceReconciliationBackgroundService>();

string jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "SuperSecretEnterpriseJwtKey_LongEnoughFor256Bits_NalunPos2026!";
string jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "NalunPosApi";
string jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "NalunPosClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Configuración de Autorización Fail-Closed: Política global que requiere autenticación por defecto
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Configuración de CORS para Frontend Apps (NalunPos-Web & NalunPos-Admin-Web)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://localhost:4201",
                  "https://localhost:4200",
                  "https://localhost:4201"
              )
              .SetIsOriginAllowed(_ => true) // Permite desarrollos locales en cualquier puerto
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configuración de Controladores con serialización de Enums como cadenas (JsonStringEnumConverter)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configuración de OpenAPI con esquema de seguridad JWT Bearer
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Ingresa tu token JWT para acceder a los endpoints protegidos."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = scheme;

        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document);

        var requirement = new OpenApiSecurityRequirement
        {
            [schemeRef] = new List<string>()
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(requirement);

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Ejecución de la siembra del SuperAdmin al arrancar la aplicación
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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

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
